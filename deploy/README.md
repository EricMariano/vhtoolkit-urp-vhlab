# Preparação do VHLab no Windows

Esta etapa prepara o repositório e um build Windows com renderização. O build ainda
não inclui streaming: publicar o executável ou iniciar um servidor HTTP não o torna
acessível pelo navegador. A seleção e integração do streaming serão uma etapa própria.

## Obter e preparar o código

```powershell
git clone --recurse-submodules git@github.com:EricMariano/vhtoolkit-urp-vhlab.git
cd vhtoolkit-urp-vhlab
git submodule update --init --recursive
```

Use a branch ou revisão contendo esta preparação após ela ser publicada. Git e acesso
de leitura ao GitHub são necessários também para os pacotes RIDE. O submódulo
`RideServices` contém serviços opcionais; não é o `rag-inct`.

Instale pelo Unity Hub a versão exata de `VHUnityURP/ProjectSettings/ProjectVersion.txt`
(atualmente `6000.5.11f1`) com **Windows Build Support**. Ative a licença do Editor,
abra `VHUnityURP`, aguarde a importação dos assets e a resolução dos pacotes. Os pacotes
RIDE estão fixados por commit em `Packages/manifest.json`; não edite `Library/PackageCache`.

## Build reproduzível

Feche o projeto no Editor antes de executar:

```powershell
powershell -NoProfile -File .\scripts\windows\Build-Lab.ps1 -PreflightOnly
powershell -NoProfile -File .\scripts\windows\Build-Lab.ps1
```

Para Editor instalado em outro local, passe `-UnityPath 'D:\Unity\Editor\Unity.exe'`.
`-OutputDirectory` muda a pasta de saída e `-Development` habilita build de desenvolvimento.
Também há o menu **VHLab > Build Windows x64** no Editor.

O script usa as cenas habilitadas em `EditorBuildSettings` (atualmente `SampleScene`)
e força o subtarget **Player**, necessário para renderizar o avatar. Mantém as configurações de assets/Addressables
do projeto; confirme que os personagens usados estão incluídos ou acessíveis no destino.

A saída fica em `artifacts/windows/`, com log `build.log`. Copie a pasta inteira,
incluindo `VHLab.exe`, DLLs e `VHLab_Data`. A máquina que somente executa o build não
precisa do Editor. Valide personagem, animação, voz e carregamento dos assets no Windows.

## Usar os campos existentes do RIDE

A integração aproveitará o provedor ChatGPT existente, sem adicionar um novo conector
de NLP ao Unity. Os campos não são intercambiáveis:

- `openAIChatGPT.endpoint`: URL completa usada pelo provedor no Editor e build Windows.
- `openAIChatGPT.endpointKey`: token enviado como `Authorization: Bearer ...` no Windows.
- `restApi.openAIProxyEndpoint`: URL completa usada por esse provedor no build WebGL.
  Nesse caminho o código atual não envia o cabeçalho Authorization do provedor.
- `restApi.url` + `stage`: base para serviços RIDE que chamam `GetRideRestApi(functionName)`.
- `rest.address` + `port`: configuração legada para serviços REST hospedados pela aplicação;
  não redireciona automaticamente o provedor de linguagem.

`ride.lab.example.json` é um **fragmento de referência**, com URLs propostas para uma
integração futura, não uma configuração pronta. O `rag-inct` atual expõe `/api/chat`,
recebe `query` e devolve `answer`. O provedor RIDE envia `messages` e espera
`choices[].message.content`. Precisaremos adaptar o backend ao contrato existente;
essas rotas de compatibilidade ainda não foram implementadas.

Configure os campos necessários pelo menu **Ride > Config...** na máquina de destino.
O arquivo persistido fica normalmente em
`%USERPROFILE%\AppData\LocalLow\USC-ICT\VHToolkit URP\config\ride.json`.
Não substitua o arquivo completo pelo fragmento e não versione tokens ou chaves reais.
No modo de conversa **Classic**, o provedor **ChatGPT** passa por esse fluxo; o modo
Unified/Realtime tem outro caminho. ASR e TTS continuam exigindo suas configurações.

## Organização com o backend

Mantenha `rag-inct` (remoto `EricMariano/multimodal-rag`) em um clone separado.
Na primeira etapa, a API escuta somente `127.0.0.1:8000`, acessível ao aplicativo
Windows na mesma máquina. Antes de disponibilizar o acesso público serão necessários
HTTPS, controle de acesso, gestão de sessões e a infraestrutura de streaming/TURN.
Mais de cinco sessões exigem teste de carga com avatar, encoder de vídeo e IA juntos.
