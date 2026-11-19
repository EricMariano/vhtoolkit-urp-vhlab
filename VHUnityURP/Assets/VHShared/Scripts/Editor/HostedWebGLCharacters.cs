using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ride;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Networking;

namespace VHLab.Editor
{
    // Download with the Editor's working configuration, never publish ride.json or signed URLs.
    public static class HostedWebGLCharacters
    {
        public const string Menu = "VHLab/WebGL/Prepare hosted characters";
        private const string Bucket = "ride-content-e22e17c3/";
        private static bool running;
        private static string Root => Path.Combine(Application.streamingAssetsPath, "Characters");
        private static string Postfix => AssetCatalogUtility.GetCompatibleUnityVersionName() + "/URP/WebGL";

        private static CatalogLoadInfoUnity[] SceneCatalogs()
        {
            var loader = Resources.FindObjectsOfTypeAll<AssetLoadingSystemAssetBundles>()
                .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded);
            if (loader == null)
                throw new InvalidOperationException("Open SampleScene before preparing hosted characters.");
            var selected = loader.m_catalogsToLoad.Where(x => x != null && x.catalogJsonFile == null &&
                (x.catalogPath == Bucket + "AssetCatalogs/VH-Internal/Maria" ||
                 x.catalogPath == Bucket + "AssetCatalogs/VH-Internal/Rashid")).ToArray();
            if (selected.Length != 2 || selected.Select(x => x.catalogPath).Distinct().Count() != 2)
                throw new InvalidOperationException("SampleScene must contain the Maria and Rashid catalogs.");
            return selected;
        }

        [MenuItem(Menu)]
        public static async void Prepare()
        {
            if (running) return;
            running = true;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Stop Play mode before downloading.");
                var catalogs = SceneCatalogs();
                if (catalogs.Length == 0) throw new InvalidOperationException("No supported scene catalogs found.");
                var config = ConfigurationSystemUnity.Load(ConfigurationSystemUnity.GetDefaultPath());
                foreach (var info in catalogs)
                {
                    string remoteFolder = info.catalogPath.TrimEnd('/') + "/" + Postfix;
                    string relativeFolder = remoteFolder.Substring(Bucket.Length);
                    string destination = Path.Combine(Root, relativeFolder);
                    Directory.CreateDirectory(destination);
                    // A failed/cancelled refresh must block builds, even when an older catalog exists.
                    string preparing = Path.Combine(destination, ".preparing");
                    File.WriteAllText(preparing, "Character download in progress. Run preparation again if interrupted.");
                    // Keep the public catalog last: interrupted downloads must not look complete.
                    string catalogFile = Path.Combine(destination, "catalog.json");
                    string pending = catalogFile + ".pending";
                    await Download(remoteFolder + "/catalog.json", pending, config);
                    var catalog = JsonUtility.FromJson<AssetCatalogData>(File.ReadAllText(pending));
                    if (catalog == null || catalog.entries == null || catalog.entries.Count == 0 ||
                        catalog.platform != "WebGL" || catalog.renderPipeline != "URP")
                        throw new InvalidDataException("Expected a nonempty URP/WebGL catalog for " + info.catalogPath);

                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string bundle in catalog.entries.Select(x => x.bundleFileName).Distinct())
                        await DownloadBundleTree(remoteFolder, destination, bundle, config, visited);
                    // RIDE deletes Unity's global manifest bundle before publishing. Build a
                    // portable dependency index from the per-bundle text manifests instead.
                    string dependencies = JsonConvert.SerializeObject(new
                    {
                        bundles = visited.OrderBy(x => x).Select(bundle => new
                        {
                            bundleFileName = bundle,
                            dependencies = Dependencies(Path.Combine(destination, bundle + ".manifest")).ToArray()
                        }).ToArray()
                    }, Formatting.Indented);
                    File.WriteAllText(Path.Combine(destination, "dependencies.json"), dependencies);
                    ValidateFolder(destination, catalog);
                    if (File.Exists(catalogFile)) File.Delete(catalogFile);
                    File.Move(pending, catalogFile);
                    File.Delete(preparing);
                    Debug.Log("[HostedCharacters] Ready: " + relativeFolder + " (" + catalog.entries.Count + " entries)");
                }
                AssetDatabase.Refresh();
                Debug.Log("[HostedCharacters] All scene characters prepared (Maria and Rashid only). Build WebGL, then upload the ENTIRE output, including StreamingAssets/Characters.");
            }
            catch (OperationCanceledException) { Debug.LogWarning("[HostedCharacters] Download cancelled. Run the menu again to resume."); }
            catch (Exception ex) { Debug.LogError("[HostedCharacters] " + ex.Message); }
            finally { running = false; EditorUtility.ClearProgressBar(); }
        }

        private static async Task DownloadBundleTree(string remote, string folder, string bundle,
            RideConfig config, HashSet<string> visited)
        {
            ValidateBundleName(bundle);
            if (!visited.Add(bundle)) return;
            string file = Path.Combine(folder, bundle);
            string manifest = file + ".manifest";
            string pending = manifest + ".pending";
            await Download(remote + "/" + bundle + ".manifest", pending, config);
            bool unchanged = File.Exists(manifest) && File.ReadAllText(manifest) == File.ReadAllText(pending);
            if (!unchanged || !File.Exists(file) || new FileInfo(file).Length == 0)
                await Download(remote + "/" + bundle, file, config);
            if (File.Exists(manifest)) File.Delete(manifest);
            File.Move(pending, manifest);
            foreach (string dependency in Dependencies(manifest))
                await DownloadBundleTree(remote, folder, dependency, config, visited);
        }

        private static IEnumerable<string> Dependencies(string manifest)
        {
            bool inDependencies = false;
            foreach (string line in File.ReadLines(manifest))
            {
                if (line.StartsWith("Dependencies:", StringComparison.Ordinal))
                {
                    inDependencies = true;
                    if (line.Contains("[]") || line.Contains("{}")) yield break;
                    continue;
                }
                if (!inDependencies || string.IsNullOrWhiteSpace(line)) continue;
                var match = Regex.Match(line, @"^\s*(?:-\s*|Dependency_\d+:\s*)(.+?)\s*$");
                if (!match.Success) yield break;
                string name = match.Groups[1].Value.Trim('\"', '\'');
                ValidateBundleName(name);
                yield return name;
            }
        }

        private static void ValidateBundleName(string name)
        {
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.Contains("..") || name.Contains("\\"))
                throw new InvalidDataException("Unsafe or empty bundle filename in catalog.");
        }

        private static async Task<string> ResolveUrl(string location, RideConfig config)
        {
            if (!ConfigurationSystemUnity.IsTerrainKeyFormatValid(config.awsTerrain.cognitoIdentityPoolId))
                return "https://ride-content-e22e17c3.s3.amazonaws.com/" + location.Substring(Bucket.Length);
            if (string.IsNullOrWhiteSpace(config.restApi.signedUrlEndpoint))
                throw new InvalidOperationException("Configure the storage signed URL endpoint in Ride > Config.");
            string json = JsonConvert.SerializeObject(new
            {
                location, authenticationKey = config.awsTerrain.cognitoIdentityPoolId,
                region = ConfigurationSystemUnity.GetTerrainKeyRegion(config), method = "GET"
            });
            using var request = new UnityWebRequest(config.restApi.signedUrlEndpoint, "PUT");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            await Send(request, "Requesting download access");
            var response = JsonConvert.DeserializeObject<GetPreSignedUrlResponse>(request.downloadHandler.text);
            string url = response?.responseData.url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new InvalidDataException("Storage signer did not return an HTTPS download URL.");
            return url;
        }

        private static async Task Download(string location, string destination, RideConfig config)
        {
            string partial = destination + ".download";
            try
            {
                string url = await ResolveUrl(location, config);
                using var request = UnityWebRequest.Get(url);
                request.downloadHandler = new DownloadHandlerFile(partial) { removeFileOnAbort = true };
                await Send(request, Path.GetFileName(destination));
                if (!File.Exists(partial) || new FileInfo(partial).Length == 0)
                    throw new InvalidDataException("Empty download: " + Path.GetFileName(destination));
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(partial, destination);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }

        private static async Task Send(UnityWebRequest request, string label)
        {
            request.timeout = 600;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Preparing WebGL characters", label, request.downloadProgress))
                {
                    request.Abort();
                    throw new OperationCanceledException();
                }
                await Task.Delay(100);
            }
            if (request.result != UnityWebRequest.Result.Success)
                // Never log the signed URL or provider response, which may contain credentials.
                throw new IOException(label + ": HTTP " + request.responseCode + " (" + request.result + "). Check network access and Ride configuration.");
        }

        private static void ValidateFolder(string folder, AssetCatalogData catalog)
        {
            var queue = new Queue<string>(catalog.entries.Select(x => x.bundleFileName));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (queue.Count > 0)
            {
                string bundleName = queue.Dequeue();
                ValidateBundleName(bundleName);
                if (!visited.Add(bundleName)) continue;
                string file = Path.Combine(folder, bundleName);
                if (!File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new BuildFailedException("Missing character bundle: " + file);
                using var stream = File.OpenRead(file);
                var header = new byte[7];
                if (stream.Read(header, 0, header.Length) != header.Length || Encoding.ASCII.GetString(header) != "UnityFS")
                    throw new BuildFailedException("Not a UnityFS bundle: " + file + ". Delete it and prepare again.");
                string manifestPath = file + ".manifest";
                if (!File.Exists(manifestPath)) throw new BuildFailedException("Missing dependency manifest: " + manifestPath);
                foreach (string dependency in Dependencies(manifestPath)) queue.Enqueue(dependency);
            }
            string index = Path.Combine(folder, "dependencies.json");
            if (!File.Exists(index) || new FileInfo(index).Length == 0)
                throw new BuildFailedException("Missing hosted dependency index in " + folder);
        }

        public static void ValidatePreparedFiles()
        {
            if (running) throw new BuildFailedException("Wait for character preparation to finish.");
            var catalogs = SceneCatalogs();
            if (catalogs.Length == 0) throw new BuildFailedException("No scene catalogs found.");
            foreach (var info in catalogs)
            {
                string folder = Path.Combine(Root, info.catalogPath.Substring(Bucket.Length), Postfix);
                if (File.Exists(Path.Combine(folder, ".preparing")))
                    throw new BuildFailedException("Character preparation is incomplete. Run " + Menu + " again.");
                string path = Path.Combine(folder, "catalog.json");
                if (!File.Exists(path)) throw new BuildFailedException("Run " + Menu + " first. Missing: " + path);
                var catalog = JsonUtility.FromJson<AssetCatalogData>(File.ReadAllText(path));
                if (catalog == null || catalog.entries == null || catalog.entries.Count == 0 ||
                    catalog.platform != "WebGL" || catalog.renderPipeline != "URP")
                    throw new BuildFailedException("Invalid URP/WebGL catalog: " + path);
                ValidateFolder(folder, catalog);
            }
        }
    }

    public sealed class HostedWebGLCharacterBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
                HostedWebGLCharacters.ValidatePreparedFiles();
        }
    }
}
