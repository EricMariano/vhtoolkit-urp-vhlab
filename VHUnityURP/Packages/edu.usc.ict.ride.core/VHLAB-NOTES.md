# VHLab embedded RIDE Core

Source: the resolved `edu.usc.ict.ride.core@33511175ca63` package, pinned in the
project manifest to RIDE commit `6daea6a891764eeede4f82cc73289d1d973b22e8`.
Unity resolves this embedded package before the Git dependency in manifest.json.
Do not edit Library/PackageCache.

Local changes are confined to Runtime/AssetCatalog/AssetLoadingSystemAssetBundles.cs:
- WebGL players mirror only Maria and Rashid scene catalog paths under StreamingAssets/Characters.
- HTTP(S) catalog/bundle URLs bypass the AWS URL signer.
- Bundles for an HTTP(S) catalog resolve beside that catalog, ignoring its old S3 prefixes.
- HTTP(S) hosted catalogs load bundle dependencies from their generated dependencies.json index.
- Native players and the Editor keep the existing scene/AWS configuration.

Prepare all scene catalogs with VHLab > WebGL > Prepare hosted characters before building.
