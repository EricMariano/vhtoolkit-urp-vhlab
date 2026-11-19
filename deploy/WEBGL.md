# WebGL com personagens na mesma hospedagem

O player WebGL carrega os catálogos e bundles de `StreamingAssets/Characters`, no
mesmo domínio do `index.html`. Assim, o navegador não precisa acessar o S3 nem o
assinador da AWS para carregar personagens. A configuração nativa/Editor continua
usando os catálogos remotos da cena.

## Preparar e publicar

1. Abra `VHUnityURP` na versão do Unity indicada em `ProjectSettings/ProjectVersion.txt`.
2. Abra `Assets/Scenes/SampleScene.unity` e saia do Play mode.
3. Execute **VHLab > WebGL > Prepare hosted characters**.
4. Aguarde a mensagem `[HostedCharacters] All scene characters prepared` na Console.
5. Selecione a plataforma **Web** e gere o build normalmente.
6. No FileZilla, selecione **Transferência > Tipo de transferência > Binário (Binary)**.
   Não use ASCII ou Automático: os bundles do RIDE não têm extensão e podem ser
   tratados como texto, corrompendo o conteúdo ao remover bytes CR (`0D`).
7. Envie **todo o conteúdo da saída** pelo FileZilla para `vhtoolkit-vhlab`, incluindo
   `Build`, `index.html`, `TemplateData` e **StreamingAssets**.

O comando baixa somente Maria (`ccMaria`) e Rashid (`ccMaleMiddleEasternRetired`).
O player WebGL inicia na Maria e oferece somente esses dois personagens. Kevin, Ellie
e Rocketbox não são baixados nem carregados no navegador. O Editor e os players nativos
mantêm a seleção original. O download usa arquivos WebGL mesmo com macOS ativo no Editor.

A estrutura gerada inclui, por exemplo:

- `StreamingAssets/Characters/AssetCatalogs/VH-Internal/Maria/6000.5.x/URP/WebGL/catalog.json`
- `StreamingAssets/Characters/AssetCatalogs/VH-Internal/Maria/6000.5.x/URP/WebGL/dependencies.json`
- Os bundles e manifests da Maria nessa mesma pasta; o mesmo para Rashid e os demais grupos.

O arquivo `dependencies.json` é gerado pela preparação usando os manifests de cada bundle.
Ele deve ser enviado junto dos catálogos e bundles. O RIDE remove o manifest global `WebGL`
ao publicar os arquivos originais, então esse arquivo não é necessário.

## Downloads e credenciais

A preparação usa apenas no Editor a configuração existente em **Ride > Config**.
Ela solicita URLs assinadas quando existe configuração válida de Cognito; caso contrário,
tenta as URLs públicas, como o carregador original. O computador precisa ter acesso aos
catálogos e aos arquivos originais. Se a AWS negar o download, o erro precisa ser resolvido
com acesso válido antes da publicação.

O comando não copia `ride.json`, chaves de API nem URLs assinadas para StreamingAssets.
Não envie a configuração pessoal para a hospedagem. Os arquivos baixados são conteúdo
publicado aos visitantes do site; preserve as condições de uso dos modelos.

Downloads são escritos em arquivos temporários. Preparações interrompidas bloqueiam o
build; execute o comando novamente para continuar. Bundles cujo manifest não mudou são
reutilizados. O build WebGL também verifica se todos os catálogos e bundles necessários
estão preparados, incluindo dependências recursivas.

Os arquivos gerados em `Assets/StreamingAssets/Characters` são ignorados pelo Git.
Em outro computador, execute novamente a preparação antes de gerar um build.

## Implementação

O pacote RIDE Core foi incorporado em `Packages/edu.usc.ict.ride.core`, seguindo o
mecanismo de embedded packages do Unity. As modificações ficam versionáveis no projeto,
e não em `Library/PackageCache`. Veja `VHLAB-NOTES.md` nesse pacote.

Somente no player WebGL, os caminhos S3 da cena são remapeados para StreamingAssets.
O carregador aceita URLs HTTP(S) diretamente, resolve os bundles ao lado do catálogo
e carrega suas dependências com o índice `dependencies.json`. Não recorre à AWS se faltar um
arquivo publicado: nesse caso a Console informa a falha HTTP da própria hospedagem.

ASR, NLP e TTS são configurações separadas; esta mudança resolve a distribuição dos
personagens, não os endpoints de IA.

## Verificar no navegador

Abra o site em uma janela anônima após o upload. Na aba Network, filtre por
`catalog.json`: as requisições devem apontar para o próprio domínio, por exemplo
`https://inct-simai.com.br/vhtoolkit-vhlab/StreamingAssets/Characters/...`.
A Console deve mostrar dois catálogos carregados: Maria e Rashid.
Selecione Maria e Rashid e confirme o carregamento do modelo e dos materiais.

Se aparecer `Failed to decompress data for the AssetBundle` com HTTP 200, confira
o tamanho do arquivo no servidor e no computador. Um upload em ASCII pode manter
o cabeçalho `UnityFS`, mas corromper o restante do arquivo. Reenvie toda a pasta
`StreamingAssets/Characters` em modo **Binário**, substituindo os arquivos; não é
necessário refazer o build. Teste sem os dados em cache da versão anterior.
