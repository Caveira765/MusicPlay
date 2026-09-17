# Trabalho Desenvolvimento de Software Visual

# MusicPlay
### Plataforma de Sincronização, Transferência e Backup de Playlists Musicais

---

**Integrantes da Equipe:**
- **Guilherme Dalla Stella Roncaglio** - RGM 42331048
- **Logan Gabriel de Brito** - RGM 42841046
- **Erick Borges de Lacerda** - RGM 28125991
- **Paulo César Ceccon Madureira** - RGM 42641551
- **Jonatan da Silva Vasconcelos** - RGM 45732817
- **Bruno Fão Moura** - RGM 42785421

---

**Link Trello:** https://trello.com/b/Mb4vQg2g/trabalho-software-visual

---

## 1. Escopo do Sistema

### Nome e Proposta do Sistema
- **Nome:** MusicPlay (Sincronizador & Gerenciador Universal de Playlists)
- **Proposta:** Uma aplicação visual centralizada e moderna que integra a transferência, sincronização e backup de playlists entre os principais serviços de streaming de música (**Spotify**, **YouTube / YouTube Music**, **Deezer**) e arquivos locais universais (**M3U8**, **CSV**, **JSON**), contando com motor de correspondência fonética/textual e acompanhamento de progresso em tempo real via WebSockets.

---

### Problema que Resolve / Usuário-Alvo
- **Problema:** Usuários de serviços de streaming frequentemente trocam de plataforma (ex.: do Spotify para o YouTube Music ou Deezer), mas não possuem uma forma nativa e simples de migrar suas bibliotecas musicais sem ter que buscar manualmente música por música. Ferramentas comerciais de mercado costumam impor limites rígidos (como cobrança para transferir mais de 500 faixas) ou exigir configurações burocráticas de desenvolvedor que afastam o usuário comum.
- **Usuário-Alvo:** Desenvolvedores, estudantes, ouvintes de música, DJs e criadores de conteúdo que buscam portabilidade de suas listas de reprodução, independência de fornecedor e backup seguro de suas faixas favoritas em uma interface visual fluida e intuitiva.

---

### Principais Funcionalidades (O que entra no escopo)

1. **Interface Estilo Wizard (Passo a Passo):**
   - Janela principal com navegação visual modular em 5 etapas: **Origem ➔ Seleção de Faixas ➔ Destino ➔ Monitor ao Vivo ➔ Conclusão**.
   - Tema escuro nativo (*Dark Mode Glassmorphism*) com elementos visuais responsivos, animação de equalizador de áudio e painéis com feedback visual imediato.

2. **Módulo de Integração Multi-Plataforma (Sem Chaves Obrigatórias):**
   - **Barra de Link Direto ("Cole e Carregue"):** Possibilidade de colar links públicos de playlists do Spotify, YouTube Music ou Deezer com detecção e extração automática de todas as faixas.
   - **Módulo Spotify:** Extração nativa de títulos, artistas, durações e capas via embed pública, com suporte opcional a Personal Access Token para bibliotecas privadas.
   - **Módulo YouTube / YouTube Music:** Leitura de playlists e busca de áudios/vídeos via biblioteca de alta performance (`YoutubeExplode`), sem necessidade de registro no Google Cloud e sem limite de cotas diárias.
   - **Módulo Deezer:** Integração direta com a API pública do Deezer para consulta de faixas, capas e metadados mundiais com código ISRC.
   - **Módulo de Arquivos Locais:** Importação e exportação de bibliotecas completas nos formatos universais **M3U8** (compatível com reprodutores de mídia), **CSV** (planilhas Excel) e **JSON**.
   - **Demo Library:** Catálogo de teste integrado com playlists completas prontas para execução e defesa imediata do software em aula.

3. **Motor de Correspondência Inteligente (Matching Engine):**
   - **Nível 1 (ISRC Match):** Identificação e pareamento com 100% de exatidão através do código internacional padronizado da gravação (*International Standard Recording Code*).
   - **Nível 2 (Normalização e Limpeza de Ruído):** Remoção algorítmica de sufixos de títulos como `(Remastered 2011)`, `(2013 Remaster)`, `[Official Music Video]`, `(Live)`, `(Deluxe Edition)` e `feat. X`.
   - **Nível 3 (Similaridade Textual Híbrida):** Combinação matemática entre a distância de **Levenshtein** e o índice de sobreposição de tokens **Jaccard** ponderados entre título e artista.
   - **Nível 4 (Validação Temporal de Duração):** Verificação de compatibilidade de duração em milissegundos com penalização gradual para evitar o pareamento acidental de gravações ao vivo ou edições estendidas no lugar de versões de estúdio.

4. **Módulo de Monitoramento e Comunicação em Tempo Real:**
   - Comunicação bidirecional contínua entre back-end e front-end utilizando **SignalR (WebSockets)**.
   - Atualização em tempo real de barra de progresso percentual, contadores de faixas correspondidas/não encontradas e terminal virtual com log detalhado de cada música processada.

5. **Armazenamento e Configurações Locais:**
   - Salvamento de histórico de sessão, preferências visuais e tokens opcionais de autenticação no próprio navegador (`localStorage`) e configurações estruturadas em `appsettings.json`.

---

### O que fica Fora do Escopo (Out of Scope)
- Download ou extração de arquivos de áudio binários (MP3/FLAC) protegidos por direitos autorais ou violação de DRM dos serviços de streaming (o sistema gerencia referências e metadados de playlists).
- Player de áudio completo em segundo plano para reprodução contínua de músicas sob demanda (foco na migração e backup de listas).
- Integração com provedores restritos que exigem contratos de desenvolvedor pagos corporativos (ex.: Apple MusicKit Developer Program pago).

---

### Tecnologias e Ferramentas Previstas
- **Linguagem Principal / Back-end:** C# (.NET 10 / ASP.NET Core).
- **Framework Visual (UI):** HTML5 semântico, CSS3 Moderno (Tailwind CSS, Glassmorphism e CSS Animations), JavaScript ES6+ e `@microsoft/signalr`.
- **Bibliotecas & APIs:**
  - `Microsoft.AspNetCore.SignalR`: Streaming de eventos e progresso via WebSockets em tempo real.
  - `YoutubeExplode`: Extração e busca de dados de playlists do YouTube/YouTube Music sem necessidade de chaves de API.
  - `SpotifyAPI.Web`: Biblioteca tipada para C# para manipulação da API Web do Spotify.
  - `HttpClient` / `System.Text.Json`: Consumo de APIs REST (Deezer) e serialização nativa de alta velocidade.
  - `xUnit`: Bateria de testes unitários automatizados para o motor de correspondência e exportadores de arquivo.
- **Gestão e Versionamento:** Trello (Quadro Kanban) e Git/GitHub.

---

## 2. Cronograma de Desenvolvimento

- **25/08 — Início do Desenvolvimento & Setup:** Criação da solução em C# (.NET), configuração da arquitetura em camadas do projeto e estrutura inicial do pipeline Web (Marco 0: Setup Inicial).
- **01/09 — Estrutura Visual da Aplicação:** Implementação da interface visual estilo wizard em 5 passos com tema escuro nativo (*Dark Mode Glassmorphism*) e navegação modular.
- **08/09 — Feriado / Recesso (Sem aula)**
- **15/09 — Integração com APIs de Música (Spotify e Deezer):** Implementação dos serviços em C# para leitura de playlists, extração de metadados e suporte a código ISRC.
- **22/09 — Feriado / Recesso (Sem aula)**
- **29/09 — Motor de Matching e Exportadores:** Implementação do algoritmo de correspondência inteligente (ISRC, Levenshtein, Jaccard e tolerância de tempo) e exportadores M3U8/CSV/JSON (Marco 1: Protótipo Visual e Lógico).
- **06/10 — Back-end do Módulo YouTube:** Integração do serviço em C# com `YoutubeExplode` para busca de faixas e leitura de playlists sem necessidade de chaves de API.
- **13/10 — Feriado / Recesso (Sem aula)**
- **20/10 — Monitoramento em Tempo Real com SignalR:** Implementação do `TransferHub` e integração do WebSocket no front-end para exibição de progresso ao vivo e terminal de eventos (Marco 2: MVP Funcional).
- **27/10 — Integração de Módulos e Link Direto:** Criação da barra universal de detecção automática de links de playlists e tela de resumo com links diretos de execução.
- **03/11 — Testes e Validação:** Execução da bateria de testes unitários (xUnit) com validação de casos de borda (ruídos de remasterização, tolerância de duração, parsing de CSV/M3U) (Marco 3: Validação).
- **10/11 — Ajustes Finais e Fechamento:** Tratamento de erros de rede, testes de usabilidade, refinamentos de UI e congelamento da versão final estável.
- **17/11 — Defesa Final:** Apresentação técnica e defesa do código-fonte do projeto para a banca avaliadora.

---

## 3. Critérios do Cronograma / Metodologia Ágil

- **1. Escopo Realista:** As funcionalidades (interface em wizard, integrações com Spotify, YouTube e Deezer via C#, motor de matching e SignalR) foram dimensionadas para conclusão equilibrada dentro das 9 aulas úteis de desenvolvimento previstas no calendário letivo.
- **2. Distribuição Contínua do Trabalho:** As entregas e tarefas estão distribuídas de forma incremental entre agosto e novembro, garantindo evolução constante e evitando o acúmulo de atividades nas semanas finais.
- **3. Tempo para Testes e Ajustes:** O cronograma reserva expressamente as aulas de 03/11 e 10/11 para validação de testes unitários automatizados, cobertura de erros de rede de APIs e polimento visual antes da entrega.
- **4. Fluxo Padrão no Kanban:** O quadro de gestão no Trello foi estruturado exatamente com as 5 colunas obrigatórias (**Backlog**, **A Fazer**, **Em Desenvolvimento**, **Em Revisão/Teste** e **Concluído**), contemplando todo o ciclo de vida do software.
- **5. Clareza e Responsabilidade das Tarefas:** Todos os cartões de desenvolvimento possuem escopo granular (planejados para execução em 1 a 2 aulas), detalhamento por checklists de critérios de aceitação e membros responsáveis devidamente atribuídos.
