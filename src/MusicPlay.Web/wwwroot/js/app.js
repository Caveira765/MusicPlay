// MusicPlay - Aplicação SPA com SignalR
const state = {
    currentStep: 1,
    providers: [],
    sourceProvider: null,
    destProvider: null,
    playlists: [],
    selectedPlaylist: null,
    selectedTracks: new Set(),
    sessionId: null,
    hubConnection: null,
    transferStats: { total: 0, matched: 0, failed: 0 },
    completedPlaylistUrl: null,
    exportDownloadUrl: null,
    userToken: localStorage.getItem('spotify_token') || '',
    matchedResults: []
};

// Inicialização ao carregar o DOM
document.addEventListener('DOMContentLoaded', async () => {
    initIcons();

    // Captura retorno OAuth do Spotify se existir (#access_token=...)
    if (window.location.hash) {
        const hashParams = new URLSearchParams(window.location.hash.substring(1));
        const token = hashParams.get('access_token');
        const error = hashParams.get('error');
        if (token) {
            localStorage.setItem('spotify_token', token);
            state.userToken = token;
            window.history.replaceState(null, null, window.location.pathname);
            alert('🎉 Conta do Spotify conectada com sucesso!\n\nAgora todas as playlists migradas serão criadas diretamente na sua biblioteca do Spotify.');
        } else if (error) {
            alert('Aviso do Spotify: ' + decodeURIComponent(error));
            window.history.replaceState(null, null, window.location.pathname);
        }
    }

    const tokenInput = document.getElementById('modal-spotify-token-input');
    if (tokenInput && state.userToken) {
        tokenInput.value = state.userToken;
    }
    updateSpotifyAuthStatus();
    await loadProviders();
    setupEventListeners();
});

function initIcons() {
    if (window.lucide) {
        window.lucide.createIcons();
    }
}

// 1. Carregar Provedores da API C#
async function loadProviders() {
    try {
        const res = await fetch('/api/transfer/providers');
        if (!res.ok) throw new Error('Falha ao carregar provedores');
        state.providers = await res.json();
        renderSourceProviders();
        renderDestProviders();
    } catch (err) {
        console.error('Erro ao carregar provedores:', err);
    }
}

// 2. Renderizar Cards de Origem
function renderSourceProviders() {
    const grid = document.getElementById('source-providers-grid');
    if (!grid) return;

    grid.innerHTML = state.providers
        .filter(p => p.canImport)
        .map(p => `
            <div onclick="selectSourceProvider('${p.id}')" 
                 class="provider-card glass-card p-6 flex flex-col items-center justify-center text-center relative group"
                 id="src-prov-${p.id}">
                <div class="w-16 h-16 rounded-2xl flex items-center justify-center mb-4 transition-transform group-hover:scale-110 shadow-lg"
                     style="background: ${p.accentColor}20; color: ${p.accentColor}; border: 1px solid ${p.accentColor}40;">
                    ${getProviderIconSvg(p.id)}
                </div>
                <h3 class="text-lg font-bold text-white mb-1">${p.name}</h3>
                <p class="text-xs text-slate-400 mb-4 px-2 line-clamp-2">${p.description}</p>
                <div class="mt-auto">
                    <span class="text-xs font-semibold px-3 py-1 rounded-full ${p.requiresAuth ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20' : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'}">
                        ${p.requiresAuth ? 'OAuth / Link Direto' : 'Uso Imediato'}
                    </span>
                </div>
            </div>
        `).join('');
}

// 3. Renderizar Cards de Destino
function renderDestProviders() {
    const grid = document.getElementById('dest-providers-grid');
    if (!grid) return;

    grid.innerHTML = state.providers
        .filter(p => p.canExport)
        .map(p => `
            <div onclick="selectDestProvider('${p.id}')" 
                 class="provider-card glass-card p-6 flex flex-col items-center justify-center text-center relative group"
                 id="dst-prov-${p.id}">
                <div class="w-16 h-16 rounded-2xl flex items-center justify-center mb-4 transition-transform group-hover:scale-110 shadow-lg"
                     style="background: ${p.accentColor}20; color: ${p.accentColor}; border: 1px solid ${p.accentColor}40;">
                    ${getProviderIconSvg(p.id)}
                </div>
                <h3 class="text-lg font-bold text-white mb-1">${p.name}</h3>
                <p class="text-xs text-slate-400 mb-4 px-2 line-clamp-2">${p.description}</p>
                <div class="mt-auto">
                    <span class="text-xs font-semibold px-3 py-1 rounded-full ${p.id === 'file' ? 'bg-blue-500/10 text-blue-400 border border-blue-500/20' : 'bg-purple-500/10 text-purple-400 border border-purple-500/20'}">
                        ${p.id === 'file' ? 'M3U8 / CSV / JSON' : 'Sincronizar Destino'}
                    </span>
                </div>
            </div>
        `).join('');
}

// Selecionar Provedor de Origem
async function selectSourceProvider(providerId) {
    state.sourceProvider = state.providers.find(p => p.id === providerId);
    
    // Destaque visual
    document.querySelectorAll('#source-providers-grid .provider-card').forEach(el => el.classList.remove('selected'));
    document.getElementById(`src-prov-${providerId}`)?.classList.add('selected');

    // Transição para o Passo 2
    goToStep(2);
    await loadSourcePlaylists();
}

// Selecionar Provedor de Destino
function selectDestProvider(providerId) {
    state.destProvider = state.providers.find(p => p.id === providerId);
    
    document.querySelectorAll('#dest-providers-grid .provider-card').forEach(el => el.classList.remove('selected'));
    document.getElementById(`dst-prov-${providerId}`)?.classList.add('selected');

    document.getElementById('btn-confirm-dest').disabled = false;
    document.getElementById('dest-summary-name').innerText = state.destProvider.name;
}

// 4. Carregar Playlists da Origem
async function loadSourcePlaylists() {
    const listContainer = document.getElementById('playlists-container');
    listContainer.innerHTML = `
        <div class="col-span-full flex flex-col items-center justify-center py-16 text-slate-400">
            <div class="w-10 h-10 border-4 border-emerald-500/30 border-t-emerald-500 rounded-full animate-spin mb-4"></div>
            <p>Carregando playlists de ${state.sourceProvider.name}...</p>
        </div>
    `;

    try {
        const tokenParam = state.userToken ? `&token=${encodeURIComponent(state.userToken)}` : '';
        const res = await fetch(`/api/transfer/playlists?providerId=${state.sourceProvider.id}${tokenParam}`);
        if (!res.ok) throw new Error('Erro ao buscar playlists');
        state.playlists = await res.json();
        renderPlaylistsList();
    } catch (err) {
        listContainer.innerHTML = `
            <div class="col-span-full glass-card p-8 text-center text-rose-400">
                <p class="font-semibold mb-2">Não foi possível carregar as playlists automaticamente.</p>
                <p class="text-xs text-slate-400 mb-4">Você pode colar o link direto ou enviar um arquivo abaixo.</p>
            </div>
        `;
    }
}

function renderPlaylistsList() {
    const container = document.getElementById('playlists-container');
    if (!container) return;

    if (state.playlists.length === 0) {
        container.innerHTML = `
            <div class="col-span-full glass-card p-8 text-center text-slate-400">
                <p>Nenhuma playlist encontrada. Use o campo acima para carregar via URL ou arquivo.</p>
            </div>
        `;
        return;
    }

    container.innerHTML = state.playlists.map(p => `
        <div onclick="selectPlaylist('${p.id}')" 
             class="glass-card p-4 flex items-center gap-4 cursor-pointer hover:border-emerald-500/50 transition-all ${state.selectedPlaylist?.id === p.id ? 'border-emerald-500 bg-emerald-500/10' : ''}">
            <img src="${p.coverImageUrl || 'https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=100&q=80'}" 
                 class="w-16 h-16 rounded-lg object-cover shadow" alt="${p.name}" />
            <div class="flex-1 min-w-0">
                <h4 class="text-white font-semibold truncate">${p.name}</h4>
                <p class="text-xs text-slate-400 truncate">${p.owner || 'Curador'}</p>
                <span class="inline-block mt-1 text-[11px] font-medium px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">
                    ${p.tracks?.length > 0 ? p.tracks.length : p.declaredTrackCount} faixas
                </span>
            </div>
            <div class="w-8 h-8 rounded-full border border-slate-700 flex items-center justify-center text-slate-400 group-hover:text-white">
                ➔
            </div>
        </div>
    `).join('');
}

// 5. Selecionar Playlist e Carregar Faixas
async function selectPlaylist(playlistId) {
    const playlist = state.playlists.find(p => p.id === playlistId);
    if (!playlist) return;

    state.selectedPlaylist = playlist;
    renderPlaylistsList();

    // Se já tiver as faixas, exibe imediatamente
    if (playlist.tracks && playlist.tracks.length > 0) {
        displayTracksView(playlist);
        return;
    }

    // Caso contrário, busca os detalhes da playlist na API C#
    const detailsContainer = document.getElementById('tracks-list-container');
    detailsContainer.innerHTML = `
        <div class="flex flex-col items-center justify-center py-12 text-slate-400">
            <div class="w-8 h-8 border-3 border-emerald-500/30 border-t-emerald-500 rounded-full animate-spin mb-3"></div>
            <p>Carregando faixas da playlist...</p>
        </div>
    `;

    try {
        const tokenParam = state.userToken ? `&token=${encodeURIComponent(state.userToken)}` : '';
        const res = await fetch(`/api/transfer/playlist-details?providerId=${state.sourceProvider.id}&playlistId=${encodeURIComponent(playlist.id)}${tokenParam}`);
        if (!res.ok) throw new Error('Falha ao obter faixas');
        const detailed = await res.json();
        state.selectedPlaylist = detailed;
        displayTracksView(detailed);
    } catch (err) {
        detailsContainer.innerHTML = `<p class="text-center text-rose-400 py-6">Erro ao carregar faixas: ${err.message}</p>`;
    }
}

// 6. Exibir Lista de Faixas com Seleção
function displayTracksView(playlist) {
    document.getElementById('playlist-selection-screen').classList.add('hidden');
    document.getElementById('tracks-selection-screen').classList.remove('hidden');

    document.getElementById('selected-playlist-title').innerText = playlist.name;
    document.getElementById('selected-playlist-meta').innerText = `${playlist.tracks.length} músicas • Por ${playlist.owner || 'MusicPlay'}`;
    document.getElementById('selected-playlist-cover').src = playlist.coverImageUrl || 'https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=200&q=80';

    // Seleciona todas por padrão
    state.selectedTracks = new Set(playlist.tracks.map(t => t.id));
    renderTracksTable(playlist.tracks);
    updateSelectedCount();
}

function renderTracksTable(tracks) {
    const container = document.getElementById('tracks-list-container');
    if (!container) return;

    if (tracks.length === 0) {
        container.innerHTML = `<div class="p-8 text-center text-slate-400">Nenhuma faixa encontrada nesta playlist.</div>`;
        return;
    }

    container.innerHTML = tracks.map((t, index) => {
        const isSelected = state.selectedTracks.has(t.id);
        return `
            <div class="flex items-center gap-3 p-3 hover:bg-slate-800/40 border-b border-slate-800/50 transition-colors ${isSelected ? 'bg-emerald-500/5' : 'opacity-60'}">
                <input type="checkbox" onchange="toggleTrack('${t.id}')" ${isSelected ? 'checked' : ''} 
                       class="w-4 h-4 rounded text-emerald-500 bg-slate-900 border-slate-700 focus:ring-emerald-500 cursor-pointer" />
                <span class="text-xs font-mono text-slate-500 w-6 text-right">${index + 1}</span>
                <img src="${t.artworkUrl || 'https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=80&q=80'}" 
                     class="w-10 h-10 rounded object-cover shadow-sm" alt="" />
                <div class="flex-1 min-w-0">
                    <p class="text-sm font-semibold text-white truncate">${escapeHtml(t.title)}</p>
                    <p class="text-xs text-slate-400 truncate">${escapeHtml(t.artist)} ${t.album ? `• <span class="text-slate-500">${escapeHtml(t.album)}</span>` : ''}</p>
                </div>
                ${t.isrc ? `<span class="hidden sm:inline-block text-[10px] font-mono px-2 py-0.5 rounded badge-isrc" title="International Standard Recording Code: ${t.isrc}">ISRC: ${t.isrc}</span>` : ''}
                <span class="text-xs font-mono text-slate-400 w-12 text-right">${t.formattedDuration || '3:30'}</span>
            </div>
        `;
    }).join('');
}

function toggleTrack(trackId) {
    if (state.selectedTracks.has(trackId)) {
        state.selectedTracks.delete(trackId);
    } else {
        state.selectedTracks.add(trackId);
    }
    renderTracksTable(state.selectedPlaylist.tracks);
    updateSelectedCount();
}

function toggleSelectAll(selectAll) {
    if (!state.selectedPlaylist) return;
    if (selectAll) {
        state.selectedTracks = new Set(state.selectedPlaylist.tracks.map(t => t.id));
    } else {
        state.selectedTracks.clear();
    }
    renderTracksTable(state.selectedPlaylist.tracks);
    updateSelectedCount();
}

function updateSelectedCount() {
    const count = state.selectedTracks.size;
    document.getElementById('selected-count-badge').innerText = `${count} selecionadas`;
    const btn = document.getElementById('btn-to-step-3');
    btn.disabled = count === 0;
    btn.innerText = `Continuar (${count} faixas) ➔`;
}

// 7. Iniciar Transferência com SignalR em Tempo Real
async function startTransferProcess() {
    if (!state.selectedPlaylist || state.selectedTracks.size === 0 || !state.destProvider) {
        alert('Por favor, selecione as faixas e o serviço de destino.');
        return;
    }

    const playlistNameInput = document.getElementById('dest-playlist-name-input').value.trim();
    const finalPlaylistName = playlistNameInput || `${state.selectedPlaylist.name} (Migrada)`;

    const tracksToSend = state.selectedPlaylist.tracks.filter(t => state.selectedTracks.has(t.id));
    state.matchedResults = [];

    goToStep(4);

    // Configurar e iniciar conexão SignalR
    await initSignalR();

    const requestPayload = {
        sessionId: state.sessionId,
        sourceProviderId: state.sourceProvider.id,
        destinationProviderId: state.destProvider.id,
        sourcePlaylistId: state.selectedPlaylist.id,
        destinationPlaylistName: finalPlaylistName,
        destinationPlaylistDescription: `Migrado de ${state.sourceProvider.name} para ${state.destProvider.name} via MusicPlay (.NET C#)`,
        tracks: tracksToSend,
        sourceAuthToken: state.userToken || null,
        destinationAuthToken: state.userToken || null
    };

    try {
        const res = await fetch('/api/transfer/start', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestPayload)
        });

        if (!res.ok) {
            const err = await res.text();
            throw new Error(err);
        }

        const data = await res.json();
        console.log('Transferência iniciada no backend C#:', data);
    } catch (err) {
        console.error('Erro ao iniciar transferência:', err);
        appendTerminalLog(`[ERRO FATAL] ${err.message}`, 'error');
    }
}

// Configuração do Hub SignalR
async function initSignalR() {
    state.sessionId = 'mp_' + Math.random().toString(36).substring(2, 10);
    
    // Inicializa conexão SignalR oficial
    state.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/transfer')
        .withAutomaticReconnect()
        .build();

    // Eventos do Servidor C#
    state.hubConnection.on('TransferStarted', (progress) => {
        appendTerminalLog(`[INÍCIO] Transferência iniciada: ${progress.totalTracks} faixas a processar.`, 'info');
        updateProgressBar(0, progress.totalTracks);
    });

    state.hubConnection.on('TrackProcessed', (matchResult, progress) => {
        handleTrackProcessed(matchResult, progress);
    });

    state.hubConnection.on('TransferCompleted', (progress) => {
        appendTerminalLog(`[CONCLUÍDO] ${progress.logMessage}`, 'success');
        updateProgressBar(100, progress.totalTracks);

        state.completedPlaylistUrl = progress.createdDestinationPlaylistUrl;
        state.exportDownloadUrl = progress.exportDownloadUrl;
        state.transferStats = {
            total: progress.totalTracks,
            matched: progress.matchedTracks,
            failed: progress.failedTracks
        };

        setTimeout(() => {
            goToStep(5);
            displayCompletionScreen();
        }, 1200);
    });

    state.hubConnection.on('TransferFailed', (error, progress) => {
        appendTerminalLog(`[FALHA] ${error}`, 'error');
        alert(`Erro na transferência: ${error}`);
    });

    state.hubConnection.on('LogMessage', (msg) => {
        appendTerminalLog(msg, 'log');
    });

    try {
        await state.hubConnection.start();
        console.log('SignalR Conectado! Ingressando na sessão:', state.sessionId);
        await state.hubConnection.invoke('JoinSession', state.sessionId);
    } catch (err) {
        console.error('Erro ao conectar ao SignalR Hub:', err);
        appendTerminalLog(`[AVISO] Conexão direta com SignalR não pôde ser iniciada: ${err.message}`, 'error');
    }
}

function handleTrackProcessed(matchResult, progress) {
    updateProgressBar(progress.percentage, progress.totalTracks, progress.processedTracks);

    document.getElementById('stat-matched-count').innerText = progress.matchedTracks;
    document.getElementById('stat-failed-count').innerText = progress.failedTracks;
    document.getElementById('stat-remaining-count').innerText = progress.totalTracks - progress.processedTracks;

    const src = matchResult.sourceTrack;
    const match = matchResult.matchedTrack;

    if (matchResult.isSuccess && match) {
        state.matchedResults.push(matchResult);
        appendTerminalLog(
            `✔ [${matchResult.matchType === 0 ? 'ISRC' : `${Math.round(matchResult.confidenceScore * 100)}%`}] "${src.title}" de ${src.artist} ➔ "${match.title}"`,
            'success'
        );
        addLiveMatchCard(src, match, matchResult.confidenceScore, true);
    } else {
        appendTerminalLog(
            `✖ [NÃO ENCONTRADA] "${src.title}" de ${src.artist}`,
            'warning'
        );
        addLiveMatchCard(src, null, 0, false);
    }
}

function updateProgressBar(percentage, total, current = 0) {
    const bar = document.getElementById('transfer-progress-bar');
    const text = document.getElementById('transfer-percentage-text');
    const subtitle = document.getElementById('transfer-current-track-label');

    if (bar) bar.style.width = `${percentage}%`;
    if (text) text.innerText = `${percentage}%`;
    if (subtitle && total) subtitle.innerText = `Processando ${current} de ${total} faixas...`;
}

function appendTerminalLog(message, type = 'info') {
    const term = document.getElementById('transfer-terminal-output');
    if (!term) return;

    const line = document.createElement('div');
    line.className = 'py-0.5 flex items-start gap-2 text-xs font-mono';

    const colorClass = type === 'success' ? 'text-emerald-400' :
                       type === 'error' ? 'text-rose-400' :
                       type === 'warning' ? 'text-amber-400' : 'text-slate-300';

    line.innerHTML = `
        <span class="text-slate-600">[${new Date().toLocaleTimeString()}]</span>
        <span class="${colorClass}">${escapeHtml(message)}</span>
    `;

    term.appendChild(line);
    term.scrollTop = term.scrollHeight;
}

function addLiveMatchCard(src, match, score, isSuccess) {
    const container = document.getElementById('live-matches-stream');
    if (!container) return;

    const card = document.createElement('div');
    card.className = `p-3 rounded-lg border text-xs flex items-center justify-between mb-2 animate-fadeIn ${isSuccess ? 'bg-slate-900/80 border-slate-800' : 'bg-rose-950/20 border-rose-900/40'}`;
    
    card.innerHTML = `
        <div class="flex items-center gap-3 min-w-0">
            <img src="${src.artworkUrl || 'https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=80&q=80'}" class="w-8 h-8 rounded object-cover" alt="" />
            <div class="min-w-0">
                <p class="font-semibold text-white truncate">${escapeHtml(src.title)}</p>
                <p class="text-slate-400 truncate">${escapeHtml(src.artist)}</p>
            </div>
        </div>
        <div class="text-right">
            ${isSuccess ? `
                <span class="text-[11px] font-bold text-emerald-400 px-2 py-0.5 rounded bg-emerald-500/10 border border-emerald-500/20">
                    ${Math.round(score * 100)}% match
                </span>
            ` : `
                <span class="text-[11px] font-bold text-rose-400 px-2 py-0.5 rounded bg-rose-500/10 border border-rose-500/20">
                    Não encontrada
                </span>
            `}
        </div>
    `;

    container.prepend(card);
}

// 8. Tela Final de Conclusão
function displayCompletionScreen() {
    const stats = state.transferStats;
    const rate = stats.total > 0 ? Math.round((stats.matched / stats.total) * 100) : 100;

    document.getElementById('final-total-count').innerText = stats.total;
    document.getElementById('final-matched-count').innerText = stats.matched;
    document.getElementById('final-failed-count').innerText = stats.failed;
    document.getElementById('final-success-rate').innerText = `${rate}%`;

    const openBtn = document.getElementById('btn-open-destination');
    const openBtnText = document.getElementById('btn-open-destination-text');
    const secBtn = document.getElementById('btn-open-secondary');
    const secBtnText = document.getElementById('btn-open-secondary-text');

    const destId = state.destProvider ? state.destProvider.id : '';
    const firstMatch = state.matchedResults && state.matchedResults.length > 0 ? state.matchedResults[0].matchedTrack : null;

    if (destId === 'youtube') {
        openBtnText.innerText = '▶ Abrir Fila Completa no YouTube';
        openBtn.href = state.completedPlaylistUrl;
        openBtn.classList.remove('hidden');

        if (firstMatch && firstMatch.id) {
            secBtnText.innerText = '🎧 Ouvir no YouTube Music';
            secBtn.href = `https://music.youtube.com/watch?v=${firstMatch.id}`;
            secBtn.classList.remove('hidden');
        } else {
            secBtn.classList.add('hidden');
        }
    } else if (destId === 'spotify') {
        openBtnText.innerText = '▶ Abrir no Spotify Web Player';
        openBtn.href = state.completedPlaylistUrl;
        openBtn.classList.remove('hidden');
        secBtn.classList.add('hidden');
    } else if (destId === 'deezer') {
        openBtnText.innerText = '▶ Abrir no Deezer';
        openBtn.href = state.completedPlaylistUrl;
        openBtn.classList.remove('hidden');
        secBtn.classList.add('hidden');
    } else {
        openBtnText.innerText = '📥 Baixar M3U8';
        openBtn.href = `/api/export/m3u/${state.sessionId}`;
        openBtn.classList.remove('hidden');
        secBtn.classList.add('hidden');
    }

    const copyBtn = document.getElementById('btn-copy-spotify-links');
    const tunemyBtn = document.getElementById('btn-tunemymusic');
    if (destId === 'spotify') {
        if (copyBtn) copyBtn.classList.remove('hidden');
        if (tunemyBtn) tunemyBtn.classList.remove('hidden');
    } else {
        if (copyBtn) copyBtn.classList.add('hidden');
        if (tunemyBtn) tunemyBtn.classList.add('hidden');
    }

    // Renderiza a lista interativa de faixas com links diretos
    const listContainer = document.getElementById('final-tracks-list');
    const countSpan = document.getElementById('final-list-count');
    const results = state.matchedResults || [];
    if (countSpan) countSpan.innerText = results.length;

    if (listContainer) {
        if (results.length === 0) {
            listContainer.innerHTML = '<p class="text-center text-slate-500 py-4">Nenhuma faixa correspondida.</p>';
        } else {
            listContainer.innerHTML = results.map(mr => {
                const src = mr.sourceTrack;
                const match = mr.matchedTrack;
                if (!match) return '';
                const link = match.externalUrl || '#';
                return `
                    <div class="flex items-center justify-between gap-3 p-2 hover:bg-slate-900/60 rounded transition-colors">
                        <div class="flex items-center gap-2.5 min-w-0">
                            <img src="${match.artworkUrl || src.artworkUrl || 'https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=60&q=80'}" class="w-8 h-8 rounded object-cover shadow" alt="" />
                            <div class="min-w-0">
                                <p class="font-semibold text-white truncate">${escapeHtml(match.title)}</p>
                                <p class="text-slate-400 text-[11px] truncate">${escapeHtml(match.artist)}</p>
                            </div>
                        </div>
                        <div class="flex items-center gap-2 shrink-0">
                            <span class="text-[10px] font-mono text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-1.5 py-0.5 rounded">
                                ${Math.round(mr.confidenceScore * 100)}% match
                            </span>
                            <a href="${link}" target="_blank" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-white text-[11px] font-medium border border-slate-700 transition-colors inline-flex items-center gap-1">
                                <span>Ouvir</span>
                                <span>↗</span>
                            </a>
                        </div>
                    </div>
                `;
            }).join('');
        }
    }

    // Configura links de download direto gerados pelo C#
    document.getElementById('btn-dl-m3u').href = `/api/export/m3u/${state.sessionId}`;
    const txtBtn = document.getElementById('btn-dl-txt');
    if (txtBtn) txtBtn.href = `/api/export/txt/${state.sessionId}`;
    document.getElementById('btn-dl-csv').href = `/api/export/csv/${state.sessionId}`;
    document.getElementById('btn-dl-json').href = `/api/export/json/${state.sessionId}`;
}

// Cancelar Transferência
async function cancelTransfer() {
    if (!state.sessionId) return;
    if (confirm('Deseja realmente interromper a transferência?')) {
        await fetch(`/api/transfer/cancel/${state.sessionId}`, { method: 'POST' });
        appendTerminalLog('[CANCELADO] Cancelamento enviado para o servidor.', 'warning');
    }
}

// Wizard de Navegação Passo a Passo
function goToStep(step) {
    state.currentStep = step;

    for (let i = 1; i <= 5; i++) {
        const screen = document.getElementById(`step-${i}-screen`);
        const pill = document.getElementById(`step-pill-${i}`);
        
        if (screen) {
            if (i === step) screen.classList.remove('hidden');
            else screen.classList.add('hidden');
        }

        if (pill) {
            if (i < step) {
                pill.className = 'flex items-center gap-2 text-xs font-semibold text-emerald-400 bg-emerald-500/10 px-3 py-1.5 rounded-full border border-emerald-500/30';
            } else if (i === step) {
                pill.className = 'flex items-center gap-2 text-xs font-semibold text-white bg-slate-800 px-3 py-1.5 rounded-full border border-slate-600 shadow';
            } else {
                pill.className = 'flex items-center gap-2 text-xs font-semibold text-slate-500 px-3 py-1.5 rounded-full';
            }
        }
    }

    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function resetWizard() {
    state.currentStep = 1;
    state.sourceProvider = null;
    state.destProvider = null;
    state.selectedPlaylist = null;
    state.selectedTracks.clear();
    state.sessionId = null;
    if (state.hubConnection) {
        state.hubConnection.stop();
        state.hubConnection = null;
    }

    document.getElementById('playlist-selection-screen').classList.remove('hidden');
    document.getElementById('tracks-selection-screen').classList.add('hidden');
    document.getElementById('transfer-terminal-output').innerHTML = '';
    document.getElementById('live-matches-stream').innerHTML = '';

    goToStep(1);
}

// Helpers de Upload e Link Direto
function setupEventListeners() {
    // Filtro de busca de faixas
    const trackSearchInput = document.getElementById('tracks-search-filter');
    if (trackSearchInput) {
        trackSearchInput.addEventListener('input', (e) => {
            if (!state.selectedPlaylist) return;
            const q = e.target.value.toLowerCase().trim();
            const filtered = state.selectedPlaylist.tracks.filter(t => 
                t.title.toLowerCase().includes(q) || 
                t.artist.toLowerCase().includes(q) ||
                (t.album && t.album.toLowerCase().includes(q))
            );
            renderTracksTable(filtered);
        });
    }

    // Upload de arquivo de playlist (CSV, M3U, JSON)
    const fileInput = document.getElementById('playlist-file-input');
    if (fileInput) {
        fileInput.addEventListener('change', async (e) => {
            const file = e.target.files[0];
            if (!file) return;

            const formData = new FormData();
            formData.append('file', file);

            try {
                const res = await fetch('/api/export/upload-file', {
                    method: 'POST',
                    body: formData
                });
                if (!res.ok) throw new Error('Erro ao processar arquivo.');
                const parsedPlaylist = await res.json();
                
                state.sourceProvider = state.providers.find(p => p.id === 'file') || state.providers[0];
                state.selectedPlaylist = parsedPlaylist;
                goToStep(2);
                displayTracksView(parsedPlaylist);
            } catch (err) {
                alert('Erro no upload: ' + err.message);
            }
        });
    }
}

// Carregar Playlist Colando Link Direto (Fácil Integração)
async function loadFromDirectUrl() {
    const input = document.getElementById('direct-url-input');
    const url = input ? input.value.trim() : '';
    if (!url) {
        alert('Por favor, cole o link de uma playlist do Spotify, YouTube Music ou Deezer.');
        return;
    }

    const btnText = document.getElementById('load-url-btn-text');
    const spinner = document.getElementById('load-url-spinner');
    if (btnText) btnText.innerText = 'Carregando...';
    if (spinner) spinner.classList.remove('hidden');

    try {
        const res = await fetch('/api/transfer/load-url', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url, token: state.userToken || null })
        });

        if (!res.ok) {
            const err = await res.text();
            throw new Error(err || 'Não foi possível carregar a playlist. Verifique se o link é válido e público.');
        }

        const data = await res.json();
        state.sourceProvider = state.providers.find(p => p.id === data.providerId) || state.providers[0];
        state.selectedPlaylist = data.playlist;

        goToStep(2);
        displayTracksView(data.playlist);
    } catch (err) {
        alert(err.message);
    } finally {
        if (btnText) btnText.innerText = 'Carregar Playlist';
        if (spinner) spinner.classList.add('hidden');
    }
}

function fillAndLoadUrl(url) {
    const input = document.getElementById('direct-url-input');
    if (input) {
        input.value = url;
        loadFromDirectUrl();
    }
}

function saveUserToken() {
    const input = document.getElementById('modal-spotify-token-input');
    const token = input ? input.value.trim() : '';
    if (token) {
        localStorage.setItem('spotify_token', token);
        state.userToken = token;
        alert('Token salvo com sucesso no seu navegador!');
    } else {
        localStorage.removeItem('spotify_token');
        state.userToken = '';
        alert('Token removido.');
    }
    updateSpotifyAuthStatus();
    closeApiModal();
}

function loginWithSpotify() {
    const input = document.getElementById('modal-spotify-client-id');
    const clientId = (input && input.value.trim()) || localStorage.getItem('spotify_client_id') || "";
    if (!clientId) {
        alert('Por favor, informe seu Client ID do Spotify na Central de Integrações antes de conectar.');
        openApiModal();
        return;
    }
    localStorage.setItem('spotify_client_id', clientId);
    const redirectUri = window.location.origin + '/callback';
    const scope = 'playlist-modify-public playlist-modify-private playlist-read-private user-read-private';
    const authUrl = `https://accounts.spotify.com/authorize?client_id=${encodeURIComponent(clientId)}&response_type=code&redirect_uri=${encodeURIComponent(redirectUri)}&scope=${encodeURIComponent(scope)}`;
    window.location.href = authUrl;
}

function updateSpotifyAuthStatus() {
    const badge = document.getElementById('spotify-auth-badge');
    const headerBtn = document.getElementById('header-spotify-btn-text');
    if (badge) {
        if (state.userToken) {
            badge.className = 'text-[10px] text-emerald-400 font-bold flex items-center gap-1';
            badge.innerHTML = '● Conectado';
        } else {
            badge.className = 'text-[10px] text-slate-400 font-semibold';
            badge.innerHTML = 'Não conectado';
        }
    }
    if (headerBtn) {
        if (state.userToken) {
            headerBtn.innerText = 'Spotify Conectado ✔';
        } else {
            headerBtn.innerText = 'Conectar Spotify';
        }
    }
    const clientInput = document.getElementById('modal-spotify-client-id');
    if (clientInput) {
        clientInput.value = localStorage.getItem('spotify_client_id') || "";
    }
}

function getProviderIconSvg(id) {
    switch (id) {
        case 'spotify':
            return `<svg class="w-8 h-8 fill-current" viewBox="0 0 24 24"><path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.503 17.31c-.218.358-.684.47-1.042.253-2.859-1.747-6.457-2.143-10.697-1.173-.41.094-.82-.162-.913-.572-.094-.41.162-.82.572-.913 4.643-1.06 8.618-.612 11.828 1.363.358.217.47.684.252 1.042zm1.47-3.265c-.274.444-.86.587-1.304.313-3.273-2.012-8.263-2.595-12.133-1.42-.497.151-1.026-.135-1.178-.632-.15-.497.135-1.026.633-1.178 4.417-1.34 9.91-.692 13.668 1.614.445.274.588.86.314 1.303zm.126-3.41c-3.927-2.332-10.407-2.547-14.167-1.405-.603.183-1.246-.164-1.43-.767-.183-.603.164-1.246.767-1.43 4.318-1.31 11.472-1.061 15.992 1.623.542.322.72 1.025.398 1.567-.322.542-1.025.72-1.56.382z"/></svg>`;
        case 'youtube':
            return `<svg class="w-8 h-8 fill-current" viewBox="0 0 24 24"><path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg>`;
        case 'deezer':
            return `<svg class="w-8 h-8 fill-current" viewBox="0 0 24 24"><path d="M18.8 4.2h4.4v3.1h-4.4zm0 4.7h4.4V12h-4.4zm0 4.7h4.4v3.1h-4.4zm-6.3 0h4.4v3.1h-4.4zm0-4.7h4.4V12h-4.4zm-6.2 4.7h4.4v3.1h-4.4zm12.5 4.7h4.4v3.1h-4.4zm-6.3 0h4.4v3.1h-4.4zm-6.2 0h4.4v3.1h-4.4zm-6.3 0h4.4v3.1H0z"/></svg>`;
        case 'file':
            return `<svg class="w-8 h-8 fill-none stroke-current stroke-2" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>`;
        default:
            return `<svg class="w-8 h-8 fill-none stroke-current stroke-2" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3"/></svg>`;
    }
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function sendToSpotifyViaTuneMyMusic() {
    const results = state.matchedResults || [];
    if (results.length === 0) {
        alert('Nenhuma música para transferir.');
        return;
    }

    const lines = results.map(r => {
        const m = r.matchedTrack || r.sourceTrack;
        return `${m.artist} - ${m.title}`;
    }).join('\n');

    navigator.clipboard.writeText(lines).then(() => {
        window.open('https://www.tunemymusic.com/transfer', '_blank');
        alert('✔ Lista copiada com sucesso!\n\nNa janela do TuneMyMusic:\n1. Clique em "Iniciar" / "Texto Livre" (ou escolha YouTube Music)\n2. Cole a lista de músicas (Ctrl + V)\n3. Selecione "Spotify" como destino e conecte\n\nEle cria a playlist diretamente na sua conta do Spotify!');
    }).catch(() => {
        window.open('https://www.tunemymusic.com/transfer', '_blank');
        prompt('Copie a lista abaixo (Ctrl+C) e cole no TuneMyMusic:', lines);
    });
}

function copyTracksForSpotify() {
    const results = state.matchedResults || [];
    if (results.length === 0) {
        alert('Nenhuma música para copiar.');
        return;
    }

    const lines = results.map(r => {
        const m = r.matchedTrack || r.sourceTrack;
        return `${m.artist} - ${m.title}`;
    }).join('\n');

    navigator.clipboard.writeText(lines).then(() => {
        alert('✔ Lista de músicas copiada em formato "Artista - Música"!\n\nVocê pode colar no TuneMyMusic, Soundiiz ou salvar em um arquivo de texto.');
    }).catch(() => {
        prompt('Copie a lista de músicas abaixo (Ctrl+C):', lines);
    });
}

