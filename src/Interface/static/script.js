/* ============================================================================
   IOT ANALYTICS DASHBOARD - INTERACTION & VISUALIZATION LOGIC
   ============================================================================ */

document.addEventListener('DOMContentLoaded', () => {
    // API Configuration
    const API_BASE = '/api';
    
    // State management
    let activeSensor = '';
    let activeTipo = '';
    let historyLimit = 100;
    let chartInstance = null;
    let pollerId = null;
    let lastLoadedId = 0;
    let predictedPoints = []; // To store temporary RPC prediction data
    
    // DOM Elements
    const selectSensor = document.getElementById('select-sensor');
    const selectTipo = document.getElementById('select-tipo');
    const inputLimite = document.getElementById('input-limite');
    const labelLimite = document.getElementById('limite-val');
    
    const kpiCurrentVal = document.querySelector('#kpi-current .kpi-value');
    const kpiAverageVal = document.querySelector('#kpi-average .kpi-value');
    const kpiTrendVal = document.querySelector('#kpi-trend .kpi-value');
    const kpiTrendSub = document.getElementById('kpi-trend-sub');
    const kpiRiskVal = document.querySelector('#kpi-risk .kpi-value');
    const kpiRiskSub = document.getElementById('kpi-risk-sub');
    const rpcStatusTxt = document.getElementById('rpc-status-txt');
    const rpcStatusDot = document.getElementById('rpc-dot');
    
    const chartLoadingPlaceholder = document.getElementById('chart-loading');
    const dataCountBadge = document.getElementById('data-count-badge');
    const chartSubTitle = document.getElementById('chart-sub-title');
    
    const terminalOutput = document.getElementById('terminal-output');
    const btnClearTerminal = document.getElementById('btn-clear-terminal');
    const btnGlobalRefresh = document.getElementById('btn-global-refresh');
    
    const tableBody = document.getElementById('history-table-body');
    
    const btnRunStats = document.getElementById('btn-run-stats');
    const btnRunPatterns = document.getElementById('btn-run-patterns');
    const btnRunPrevisao = document.getElementById('btn-run-previsao');
    
    const analysisModal = document.getElementById('analysis-modal');
    const btnCloseModal = document.getElementById('btn-close-modal');
    const btnModalCloseOk = document.getElementById('btn-modal-close-ok');
    const modalDetailsBody = document.getElementById('modal-details-body');
    
    const toast = document.getElementById('notification-toast');
    const toastTitle = document.querySelector('.toast-title');
    const toastDesc = document.querySelector('.toast-desc');
    const toastIcon = document.querySelector('.toast-icon');

    // ============================================================================
    // INITIALIZATION
    // ============================================================================
    
    async function init() {
        logToTerminal('A carregar filtros iniciais do SQLite...', 'system');
        
        // Setup chart
        initChart();
        
        // Initial Fetch
        await refreshFilters();
        
        // Initial check of RPC Service status
        checkRPCStatus();
        
        // Start background poller
        startPoller();
        
        // Load recent analyses
        refreshAnalysisTable();
        
        logToTerminal('Painel pronto para receber medições em tempo real.', 'system');
    }

    // ============================================================================
    // FILTERS & CORE CONFIGURATION
    // ============================================================================
    
    async function refreshFilters() {
        try {
            // Fetch active sensors
            const resSensores = await fetch(`${API_BASE}/sensores`);
            const sensores = await resSensores.json();
            
            // Save currently selected sensor
            const oldSensor = selectSensor.value;
            selectSensor.innerHTML = '';
            
            if (sensores.length === 0) {
                selectSensor.innerHTML = '<option value="">(Sem sensores ativos)</option>';
                activeSensor = '';
            } else {
                // Add option to select 'todos' for global stats, if sensors exist
                selectSensor.innerHTML += '<option value="todos">Todos os Sensores</option>';
                sensores.forEach(s => {
                    const opt = document.createElement('option');
                    opt.value = s;
                    opt.textContent = s;
                    selectSensor.appendChild(opt);
                });
                
                // Select previous or first sensor
                if (oldSensor && sensores.includes(oldSensor)) {
                    selectSensor.value = oldSensor;
                } else if (oldSensor === 'todos') {
                    selectSensor.value = 'todos';
                } else {
                    selectSensor.value = sensores[0];
                }
                activeSensor = selectSensor.value;
            }

            // Fetch active data types
            const resTipos = await fetch(`${API_BASE}/tipos`);
            const tipos = await resTipos.json();
            
            const oldTipo = selectTipo.value;
            selectTipo.innerHTML = '';
            
            if (tipos.length === 0) {
                selectTipo.innerHTML = '<option value="">(Sem tipos de dados)</option>';
                activeTipo = '';
            } else {
                tipos.forEach(t => {
                    const opt = document.createElement('option');
                    opt.value = t;
                    opt.textContent = t.charAt(0).toUpperCase() + t.slice(1);
                    selectTipo.appendChild(opt);
                });
                
                if (oldTipo && tipos.includes(oldTipo)) {
                    selectTipo.value = oldTipo;
                } else {
                    selectTipo.value = tipos[0];
                }
                activeTipo = selectTipo.value;
            }
            
            updateChartInfoText();
            await refreshData();
            
        } catch (err) {
            logToTerminal(`Erro ao carregar filtros: ${err.message}`, 'error');
        }
    }

    function updateChartInfoText() {
        if (activeSensor && activeTipo) {
            const sName = activeSensor === 'todos' ? 'Todos os Sensores' : activeSensor;
            chartSubTitle.textContent = `Visualizando dados de '${activeTipo}' | ${sName}`;
        } else {
            chartSubTitle.textContent = 'Sem medições correspondentes aos filtros selecionados.';
        }
    }

    // ============================================================================
    // DATA FETCHING & POLLING
    // ============================================================================
    
    async function refreshData() {
        if (!activeSensor || !activeTipo) {
            chartLoadingPlaceholder.classList.remove('hidden');
            return;
        }
        
        try {
            const limit = inputLimite.value;
            const url = `${API_BASE}/medicoes?tipo_dado=${activeTipo}${activeSensor !== 'todos' ? `&sensor_id=${activeSensor}` : ''}&limit=${limit}`;
            
            const res = await fetch(url);
            const data = await res.json();
            
            if (data.length === 0) {
                chartLoadingPlaceholder.classList.remove('hidden');
                chartLoadingPlaceholder.querySelector('p').textContent = 'Nenhuma medição encontrada para estes filtros.';
                chartLoadingPlaceholder.querySelector('.spinner').style.display = 'none';
                
                kpiCurrentVal.textContent = '--';
                kpiAverageVal.textContent = '--';
                
                dataCountBadge.textContent = '0 pontos';
                updateChart([], []);
                return;
            }
            
            chartLoadingPlaceholder.classList.add('hidden');
            chartLoadingPlaceholder.querySelector('.spinner').style.display = 'block';
            
            // Order chronologically (original query is desc, so we reverse it)
            data.reverse();
            
            // Extract values and labels
            const valores = data.map(m => parseFloat(m.valor));
            const timestamps = data.map(m => {
                const dt = new Date(m.timestamp);
                return dt.toLocaleTimeString('pt-PT');
            });
            
            // Check for new real-time lines to dump in the terminal
            data.forEach(m => {
                if (m.id > lastLoadedId) {
                    logToTerminal(`[DADO] Sensor: ${m.sensor_id} | Tipo: ${m.tipo_dado} | Valor: ${parseFloat(m.valor).toFixed(2)} | Hora: ${new Date(m.timestamp).toLocaleTimeString()}`, 'data');
                    if (m.id > lastLoadedId) {
                        lastLoadedId = m.id;
                    }
                }
            });
            
            // Update KPI Stats cards
            const currentVal = valores[valores.length - 1];
            kpiCurrentVal.textContent = currentVal.toFixed(2);
            
            const soma = valores.reduce((acc, curr) => acc + curr, 0);
            const media = soma / valores.length;
            kpiAverageVal.textContent = media.toFixed(2);
            
            dataCountBadge.textContent = `${valores.length} pontos`;
            
            // Render chart
            updateChart(timestamps, valores);
            
        } catch (err) {
            logToTerminal(`Erro ao carregar medições: ${err.message}`, 'error');
        }
    }

    // ============================================================================
    // CHART.JS GRAPH WRAPPER
    // ============================================================================
    
    function initChart() {
        const ctx = document.getElementById('sensorChart').getContext('2d');
        
        // Elegant styling gradients for dark mode
        const gradient = ctx.createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, 'rgba(0, 242, 254, 0.35)');
        gradient.addColorStop(1, 'rgba(0, 242, 254, 0.01)');
        
        const forecastGradient = ctx.createLinearGradient(0, 0, 0, 300);
        forecastGradient.addColorStop(0, 'rgba(255, 159, 67, 0.3)');
        forecastGradient.addColorStop(1, 'rgba(255, 159, 67, 0.01)');
        
        chartInstance = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'Medição Real',
                        data: [],
                        borderColor: '#00f2fe',
                        borderWidth: 3,
                        backgroundColor: gradient,
                        fill: true,
                        tension: 0.35,
                        pointBackgroundColor: '#00f2fe',
                        pointBorderColor: '#fff',
                        pointHoverRadius: 6,
                        pointHoverBackgroundColor: '#00f2fe',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2,
                    },
                    {
                        label: 'Previsão Futura',
                        data: [],
                        borderColor: '#ff9f43',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        backgroundColor: forecastGradient,
                        fill: true,
                        tension: 0.35,
                        pointBackgroundColor: '#ff9f43',
                        pointHoverRadius: 6,
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: '#8e95b2',
                            font: { family: 'Outfit', size: 12, weight: '500' }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(16, 18, 38, 0.95)',
                        borderColor: 'rgba(255, 255, 255, 0.1)',
                        borderWidth: 1,
                        titleColor: '#fff',
                        titleFont: { family: 'Outfit', weight: '600' },
                        bodyColor: '#fff',
                        bodyFont: { family: 'Outfit' },
                        padding: 12,
                        cornerRadius: 10,
                        displayColors: true
                    }
                },
                scales: {
                    x: {
                        grid: { color: 'rgba(255, 255, 255, 0.03)' },
                        ticks: { color: '#8e95b2', font: { family: 'Space Grotesk', size: 10 } }
                    },
                    y: {
                        grid: { color: 'rgba(255, 255, 255, 0.03)' },
                        ticks: { color: '#8e95b2', font: { family: 'Space Grotesk', size: 10 } }
                    }
                }
            }
        });
    }

    function updateChart(labels, values) {
        if (!chartInstance) return;
        
        chartInstance.data.labels = [...labels];
        chartInstance.data.datasets[0].data = [...values];
        
        // Handle predicted future points if any
        if (predictedPoints.length > 0) {
            const extendedLabels = [...labels];
            const datasetForecast = new Array(values.length).fill(null);
            
            // Join the last real point to the first predicted point for visual continuity
            datasetForecast[values.length - 1] = values[values.length - 1];
            
            predictedPoints.forEach((p, idx) => {
                extendedLabels.push(`Previsão +${idx + 1}`);
                datasetForecast.push(p);
            });
            
            chartInstance.data.labels = extendedLabels;
            chartInstance.data.datasets[1].data = datasetForecast;
        } else {
            chartInstance.data.datasets[1].data = [];
        }
        
        chartInstance.update();
    }

    // ============================================================================
    // RPC ANALYSIS TRIGGERS
    // ============================================================================
    
    async function triggerAnalysis(type) {
        if (!activeSensor || !activeTipo) {
            showToast('Erro', 'Por favor, selecione um sensor e tipo de dado válidos.', 'error');
            return;
        }
        
        const btn = document.querySelector(`.btn-analysis[data-type="${type}"]`);
        btn.classList.add('loading');
        
        const typeLabels = {
            'estatisticas': 'Estatísticas Avançadas',
            'padroes': 'Deteção de Padrões',
            'previsao': 'Previsão de Risco'
        };
        
        logToTerminal(`[RPC] A enviar pedido de ${typeLabels[type]} (Sensor: ${activeSensor} | Tipo: ${activeTipo})...`, 'rpc');
        
        try {
            const response = await fetch(`${API_BASE}/analise`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sensor_id: activeSensor,
                    tipo_dado: activeTipo,
                    tipo_analise: type
                })
            });
            
            const result = await response.json();
            btn.classList.remove('loading');
            
            if (!result.sucesso) {
                logToTerminal(`[RPC ERRO] Falha no cálculo: ${result.erro}`, 'error');
                showToast('Erro na Análise', result.erro, 'error');
                return;
            }
            
            logToTerminal(`[RPC SUCESSO] ${typeLabels[type]} concluída e persistida com sucesso no SQLite!`, 'rpc');
            showToast('Análise Concluída', `${typeLabels[type]} gerada com sucesso.`, 'success');
            
            // If it is a prediction, update the visual charts
            if (type === 'previsao' && result.previsoes) {
                predictedPoints = result.previsoes;
                refreshData(); // Redraws chart with forecast
            } else {
                predictedPoints = []; // reset if other analytics are chosen
            }
            
            // Dynamic Updates to Dashboard KPIs based on RPC results
            if (result.tendencia) {
                kpiTrendVal.textContent = formatTrend(result.tendencia);
                kpiTrendSub.textContent = `Atualizado por RPC ${typeLabels[type]}`;
                
                // Color transition
                if (result.tendencia.toLowerCase().includes('subindo')) {
                    kpiTrendVal.className = 'kpi-value text-glow';
                } else if (result.tendencia.toLowerCase().includes('descendo')) {
                    kpiTrendVal.className = 'kpi-value text-glow-red';
                } else {
                    kpiTrendVal.className = 'kpi-value';
                }
            }
            
            if (result.risco) {
                kpiRiskVal.textContent = result.risco.toUpperCase();
                kpiRiskSub.textContent = `Amostra: Próximo valor ~${result.proximo_valor ? result.proximo_valor.toFixed(2) : '--'}`;
                
                // Color risk
                if (result.risco.toLowerCase() === 'alto') {
                    kpiRiskVal.className = 'kpi-value text-glow-red';
                } else if (result.risco.toLowerCase() === 'medio') {
                    kpiRiskVal.className = 'kpi-value';
                    kpiRiskVal.style.color = 'var(--accent-orange)';
                } else {
                    kpiRiskVal.className = 'kpi-value';
                    kpiRiskVal.style.color = 'var(--accent-green)';
                }
            }
            
            // Pop open details Modal
            displayAnalysisDetailsModal(type, result);
            
            // Refresh DB Analysis Table
            refreshAnalysisTable();
            
        } catch (err) {
            btn.classList.remove('loading');
            logToTerminal(`Erro de processamento: ${err.message}`, 'error');
            showToast('Erro Fatal', `Conexão falhou: ${err.message}`, 'error');
        }
    }

    function formatTrend(trend) {
        if (trend === 'subindo') return 'SUBIDA ▲';
        if (trend === 'descendo') return 'DESCENTE ▼';
        if (trend === 'estavel') return 'ESTÁVEL ▬';
        return trend.toUpperCase();
    }

    // ============================================================================
    // MODAL DETAIL VIEWS
    // ============================================================================
    
    function displayAnalysisDetailsModal(type, res) {
        modalDetailsBody.innerHTML = '';
        
        let contentHTML = `
            <div class="detail-header-desc">
                <p><strong>Sensor:</strong> ${res.sensor_id} | <strong>Tipo de Dado:</strong> ${res.tipo_dado}</p>
                <p class="modal-time-desc">Calculado em: ${new Date().toLocaleTimeString()} (UTC)</p>
            </div>
            <div class="separator" style="margin: 8px 0"></div>
        `;
        
        if (type === 'estatisticas') {
            contentHTML += `
                <h4><i class="fa-solid fa-square-poll-vertical"></i> Valores Estatísticos Calculados</h4>
                <div class="detail-grid">
                    <div class="detail-item">
                        <span class="detail-lbl">Amostras</span>
                        <span class="detail-val">${res.count}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Média</span>
                        <span class="detail-val">${res.media.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Mediana</span>
                        <span class="detail-val">${res.mediana.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Desvio Padrão</span>
                        <span class="detail-val">${res.desvio_padrao.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Variância</span>
                        <span class="detail-val">${res.variancia.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Mínimo / Máximo</span>
                        <span class="detail-val">${res.minimo.toFixed(2)} / ${res.maximo.toFixed(2)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Quartil Q1</span>
                        <span class="detail-val">${res.q1.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Quartil Q3</span>
                        <span class="detail-val">${res.q3.toFixed(3)}</span>
                    </div>
                </div>
            `;
        } else if (type === 'padroes') {
            const anomalias = res.anomalias || [];
            
            contentHTML += `
                <div class="detail-grid">
                    <div class="detail-item">
                        <span class="detail-lbl">Total Anomalias</span>
                        <span class="detail-val" style="color: ${anomalias.length > 0 ? 'var(--accent-red)' : 'var(--accent-green)'}">
                            ${res.total_anomalias}
                        </span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Tendência Geral</span>
                        <span class="detail-val">${formatTrend(res.tendencia)}</span>
                    </div>
                </div>
                
                <h4 style="margin-top: 12px;"><i class="fa-solid fa-triangle-exclamation"></i> Anomalias Detetadas (Z-Score > 1.96)</h4>
            `;
            
            if (anomalias.length === 0) {
                contentHTML += `
                    <div class="anomalia-item" style="background: rgba(0, 245, 160, 0.08); border-color: rgba(0, 245, 160, 0.2);">
                        <span style="color: var(--accent-green); font-size: 13px; font-weight: 500;">
                            <i class="fa-solid fa-circle-check"></i> Sem anomalias detetadas. Valores normativos estáveis.
                        </span>
                    </div>
                `;
            } else {
                contentHTML += `<div class="anomalia-list">`;
                anomalias.forEach(anom => {
                    contentHTML += `
                        <div class="anomalia-item">
                            <div class="anomalia-item-info">
                                <span class="anomalia-title">Índice #${anom.indice}</span>
                                <span class="anomalia-desc">${anom.descricao || 'Z-Score acima do limite estatístico'}</span>
                            </div>
                            <span class="anomalia-val-badge">
                                Valor: ${anom.valor.toFixed(2)} (z=${anom.zscore.toFixed(2)})
                            </span>
                        </div>
                    `;
                });
                contentHTML += `</div>`;
            }
        } else if (type === 'previsao') {
            const prevs = res.previsoes || [];
            
            contentHTML += `
                <div class="detail-grid">
                    <div class="detail-item">
                        <span class="detail-lbl">Próximo Valor Estimado</span>
                        <span class="detail-val" style="color: var(--accent-blue)">~${res.proximo_valor.toFixed(3)}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-lbl">Nível de Risco</span>
                        <span class="detail-val" style="color: ${res.risco === 'alto' ? 'var(--accent-red)' : 'var(--accent-green)'}">
                            ${res.risco.toUpperCase()}
                        </span>
                    </div>
                </div>
                
                <h4 style="margin-top: 12px;"><i class="fa-solid fa-clock-rotate-left"></i> Próximos 5 Valores Projetados</h4>
                <div class="detail-grid" style="grid-template-columns: repeat(5, 1fr); padding: 12px; text-align: center;">
            `;
            
            prevs.forEach((p, i) => {
                contentHTML += `
                    <div class="detail-item" style="align-items: center;">
                        <span class="detail-lbl">T+${i+1}</span>
                        <span class="detail-val" style="font-size: 13px;">${p.toFixed(2)}</span>
                    </div>
                `;
            });
            
            contentHTML += `
                </div>
                <div class="separator" style="margin: 8px 0"></div>
                <p style="font-size: 12px; color: var(--text-muted); text-align: center;">
                    <em>A curva de regressão linear está agora delineada em tracejado cor-de-laranja no gráfico temporal principal.</em>
                </p>
            `;
        }
        
        modalDetailsBody.innerHTML = contentHTML;
        analysisModal.classList.remove('hidden');
    }

    // ============================================================================
    // RECENT ANALYSIS HISTORY TABLE
    // ============================================================================
    
    async function refreshAnalysisTable() {
        try {
            const res = await fetch(`${API_BASE}/analises`);
            const analises = await res.json();
            
            tableBody.innerHTML = '';
            
            if (analises.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="table-empty">Sem registos de análises executadas.</td>
                    </tr>
                `;
                return;
            }
            
            analises.forEach(a => {
                const tr = document.createElement('tr');
                
                const time = new Date(a.timestamp);
                const timeStr = `${time.toLocaleDateString()} ${time.toLocaleTimeString()}`;
                
                // Formulate summary text based on analysis type
                let resumo = '--';
                const r = a.resultado;
                
                if (a.tipo_analise === 'estatisticas') {
                    resumo = `Média: ${r.media ? r.media.toFixed(2) : '--'} | Máx: ${r.maximo ? r.maximo.toFixed(2) : '--'} (N=${r.count})`;
                } else if (a.tipo_analise === 'padroes') {
                    resumo = `Anomalias: ${r.total_anomalias} | Tendência: ${r.tendencia || '--'}`;
                } else if (a.tipo_analise === 'previsao') {
                    resumo = `Próximo: ~${r.proximo_valor ? r.proximo_valor.toFixed(2) : '--'} | Risco: ${r.risco ? r.risco.toUpperCase() : '--'}`;
                }
                
                // Set badges classes
                const badgeClass = a.tipo_analise === 'estatisticas' ? 'stats' : (a.tipo_analise === 'padroes' ? 'patterns' : 'forecast');
                const badgeLabel = a.tipo_analise === 'estatisticas' ? 'Stats' : (a.tipo_analise === 'padroes' ? 'Padrões' : 'Prever');
                
                tr.innerHTML = `
                    <td>${timeStr}</td>
                    <td><span class="badge" style="color: #fff">${a.sensor_id}</span></td>
                    <td><span class="badge">${a.tipo_dado}</span></td>
                    <td><span class="badge-analise ${badgeClass}">${badgeLabel}</span></td>
                    <td style="font-family: var(--font-mono); font-size: 11px;">${resumo}</td>
                `;
                
                // Modal overlay popover on click
                tr.addEventListener('click', () => {
                    displayAnalysisDetailsModal(a.tipo_analise, a.resultado);
                });
                
                tableBody.appendChild(tr);
            });
            
        } catch (err) {
            console.error('Erro ao ler tabela de análise:', err);
        }
    }

    // ============================================================================
    // UTILITIES & DECORATORS
    // ============================================================================
    
    function logToTerminal(message, type = 'system') {
        const line = document.createElement('div');
        line.className = `terminal-line ${type}-line`;
        
        const timestamp = new Date().toLocaleTimeString('pt-PT');
        
        const prefix = type === 'system' ? '[SISTEMA]' : (type === 'data' ? '[DADO]' : (type === 'rpc' ? '[RPC]' : '[ERRO]'));
        line.textContent = `${timestamp} ${prefix} ${message}`;
        
        terminalOutput.appendChild(line);
        
        // Auto scroll
        terminalOutput.scrollTop = terminalOutput.scrollHeight;
        
        // Restrict terminal length to 200 lines to avoid high RAM use
        if (terminalOutput.childNodes.length > 200) {
            terminalOutput.removeChild(terminalOutput.firstChild);
        }
    }
    
    function showToast(title, desc, type = 'info') {
        toastTitle.textContent = title;
        toastDesc.textContent = desc;
        
        // Setup icons and border style
        toast.className = 'toast'; // reset classes
        if (type === 'success') {
            toastIcon.className = 'fa-solid fa-circle-check toast-icon';
            toastIcon.style.color = 'var(--accent-green)';
            toast.style.borderColor = 'var(--accent-green)';
        } else if (type === 'error') {
            toastIcon.className = 'fa-solid fa-circle-exclamation toast-icon';
            toastIcon.style.color = 'var(--accent-red)';
            toast.style.borderColor = 'var(--accent-red)';
        } else {
            toastIcon.className = 'fa-solid fa-circle-info toast-icon';
            toastIcon.style.color = 'var(--accent-blue)';
            toast.style.borderColor = 'var(--accent-blue)';
        }
        
        toast.classList.remove('hidden');
        
        // Dismiss after 4s
        setTimeout(() => {
            toast.classList.add('hidden');
        }, 4000);
    }
    
    async function checkRPCStatus() {
        try {
            // Check if backend is alive
            const res = await fetch(`${API_BASE}/tipos`);
            if (res.ok) {
                rpcStatusTxt.textContent = 'Ativo';
                rpcStatusTxt.style.color = 'var(--accent-green)';
                rpcStatusDot.className = 'status-dot green';
            } else {
                rpcStatusTxt.textContent = 'Erro';
                rpcStatusTxt.style.color = 'var(--accent-red)';
                rpcStatusDot.className = 'status-dot red';
            }
        } catch (e) {
            rpcStatusTxt.textContent = 'Offline';
            rpcStatusTxt.style.color = 'var(--accent-red)';
            rpcStatusDot.className = 'status-dot red';
        }
    }

    // Poller management
    function startPoller() {
        if (pollerId) clearInterval(pollerId);
        
        pollerId = setInterval(async () => {
            await refreshData();
            await refreshAnalysisTable();
        }, 3000); // Polls every 3 seconds
    }
    
    // ============================================================================
    // INTERFACES & LISTENERS
    // ============================================================================
    
    selectSensor.addEventListener('change', (e) => {
        activeSensor = e.target.value;
        predictedPoints = []; // reset forecast lines
        updateChartInfoText();
        refreshData();
    });
    
    selectTipo.addEventListener('change', (e) => {
        activeTipo = e.target.value;
        predictedPoints = []; // reset forecast lines
        updateChartInfoText();
        refreshData();
    });
    
    inputLimite.addEventListener('input', (e) => {
        labelLimite.textContent = e.target.value;
    });
    
    inputLimite.addEventListener('change', () => {
        refreshData();
    });
    
    btnClearTerminal.addEventListener('click', () => {
        terminalOutput.innerHTML = '';
        logToTerminal('Consola limpa com sucesso.', 'system');
    });
    
    btnGlobalRefresh.addEventListener('click', async () => {
        showToast('A Atualizar', 'A sincronizar dados da base de dados...', 'info');
        await refreshFilters();
        await refreshAnalysisTable();
    });
    
    // Close Modals
    const closeModal = () => {
        analysisModal.classList.add('hidden');
    };
    
    btnCloseModal.addEventListener('click', closeModal);
    btnModalCloseOk.addEventListener('click', closeModal);
    analysisModal.addEventListener('click', (e) => {
        if (e.target === analysisModal) closeModal();
    });
    
    // Attach buttons RPC triggers
    btnRunStats.addEventListener('click', () => triggerAnalysis('estatisticas'));
    btnRunPatterns.addEventListener('click', () => triggerAnalysis('padroes'));
    btnRunPrevisao.addEventListener('click', () => triggerAnalysis('previsao'));

    // Start everything
    init();
});
