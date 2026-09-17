# 🎵 MusicPlay - Clone do Tune My Music em C# (.NET 10)

O **MusicPlay** é uma aplicação completa para transferência, sincronização e backup de playlists entre serviços de música, inspirada no *Tune My Music*, construída com **backend robusto em C# (.NET 10 / ASP.NET Core)** e interface web interativa em tempo real via **SignalR (WebSockets)**.

---

## ✨ Principais Funcionalidades

1. **Multi-Plataformas Suportadas (100% Funcional sem Chaves Obrigatórias)**:
   - **Barra Universal "Cole e Transfira"**: Cole qualquer link de playlist do Spotify, YouTube Music ou Deezer na tela inicial e carregue todas as faixas instantaneamente!
   - **Spotify**: Leitura de qualquer playlist pública via extração nativa de embed e busca avançada de faixas.
   - **YouTube / YouTube Music**: Leitura de playlists e busca de vídeos/áudios via biblioteca de alta performance `YoutubeExplode` (sem necessidade de API key ou cotas).
   - **Deezer**: Leitura e busca pública ao vivo via API aberta nativa com suporte a códigos ISRC.
   - **Arquivos & Backups**: Importação e exportação nos formatos universais **M3U8**, **CSV** (Excel) e **JSON**.
   - **Demo Library Integrada**: Catálogo demonstrativo rico com faixas reais para testes com 1 clique.

2. **Motor de Correspondência Inteligente (`MatchingEngine`)**:
   - **Nível 1 (ISRC Match)**: Correspondência 100% precisa pelo código internacional padrão de gravação.
   - **Nível 2 (Normalização de Ruído)**: Remoção inteligente de sufixos como `(Remastered 2011)`, `(2013 Remaster)`, `[Official Music Video]`, `(Live)`, `feat. X`.
   - **Nível 3 (Similaridade de Strings)**: Distância de Levenshtein combinada com Jaccard Token Overlap para título e artista.
   - **Nível 4 (Validação de Duração)**: Penalização de versões ao vivo ou estendidas que diferem em mais de 40-60 segundos da faixa original.

3. **Monitoramento em Tempo Real via SignalR**:
   - Streaming WebSockets com atualizações a cada música processada.
   - Barra de progresso animada com porcentagem em tempo real.
   - Terminal virtual exibindo o log exato de cada correspondência e pontuação de confiança.
   - Stream visual com cards das músicas sincronizadas.

4. **Interface Moderna (Dark Glassmorphism)**:
   - Wizard intuitivo de 5 passos (Origem ➔ Faixas ➔ Destino ➔ Monitor ➔ Concluído).
   - Seleção em massa e filtro instantâneo de pesquisa de faixas.
   - Downloads com 1 clique de playlists em M3U8, CSV e JSON.

---

## 🚀 Como Executar

### Pré-requisitos
- [.NET SDK 10 ou superior](https://dotnet.microsoft.com/) instalado no seu computador.

### Passo a Passo

1. Abra o terminal na pasta do projeto:
   ```bash
   cd c:\Users\guiro\Downloads\MusicPlay
   ```

2. Execute a aplicação:
   ```bash
   dotnet run --project src/MusicPlay.Web
   ```

3. Abra seu navegador em:
   ```
   http://localhost:5000
   ```

---

## 🧪 Como Rodar os Testes Unitários

O projeto conta com suíte de testes unitários com **xUnit** cobrindo o algoritmo de correspondência, remoção de ruído, tolerância de duração e importadores/exportadores de arquivos:

```bash
dotnet test
```

---

## ⚙️ Configuração de Credenciais Opcionais

O MusicPlay já vem pronto para funcionar imediatamente através do **Deezer** e da **Demo Library**.

Caso queira conectar diretamente a sua própria conta do **Spotify** ou **YouTube Music**, configure suas chaves no arquivo `src/MusicPlay.Web/appsettings.json`:

```json
{
  "Spotify": {
    "ClientId": "SEU_SPOTIFY_CLIENT_ID",
    "ClientSecret": "SEU_SPOTIFY_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5000/callback"
  },
  "YouTube": {
    "ApiKey": "SUA_CHAVE_GOOGLE_YOUTUBE_API"
  }
}
```

---

## 📁 Estrutura da Solução

```
MusicPlay/
├── MusicPlay.sln
├── src/
│   └── MusicPlay.Web/
│       ├── Controllers/          # Endpoints REST (Transfer, Export, Auth)
│       ├── Hubs/                 # TransferHub (SignalR WebSockets)
│       ├── Models/               # Modelos de domínio e DTOs (Track, Playlist, MatchResult)
│       ├── Providers/            # Implementações (Spotify, YouTube, Deezer, File, Demo)
│       ├── Services/             # Motor de Matching e Orquestrador de Transferência
│       ├── wwwroot/              # Interface Web SPA (HTML5, Tailwind CSS, JS, SignalR)
│       ├── Program.cs            # Pipeline e Injeção de Dependências ASP.NET Core
│       └── appsettings.json      # Configurações de API
└── tests/
    └── MusicPlay.Tests/          # Testes com xUnit
        ├── MatchingEngineTests.cs
        └── FileProviderTests.cs
```
