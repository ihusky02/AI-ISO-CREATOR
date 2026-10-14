// AI-ISO-Creator Frontend Application Logic

let currentRecipe = null;
let currentJobId = null;
let eventSource = null;

document.addEventListener('DOMContentLoaded', () => {
    initApp();
});

async function initApp() {
    await checkStatus();
    await loadPresets();
    setupEventListeners();
}

// 1. Check system runtime status (Gemini, Podman, QEMU)
async function checkStatus() {
    try {
        const res = await fetch('/api/status');
        const data = await res.json();

        // Gemini status badge
        const geminiBadge = document.getElementById('status-gemini');
        if (data.hasApiKey) {
            geminiBadge.className = 'badge-status active';
            geminiBadge.innerHTML = `<span class="status-dot"></span> Gemini AI: ${data.selectedModel}`;
        } else {
            geminiBadge.className = 'badge-status inactive';
            geminiBadge.innerHTML = `<span class="status-dot"></span> Gemini: No API Key (Offline Mode)`;
        }

        // Podman container engine status badge
        const podmanBadge = document.getElementById('status-podman');
        if (data.isPodmanAvailable) {
            podmanBadge.className = 'badge-status active';
            podmanBadge.innerHTML = `<span class="status-dot"></span> Podman: Ready`;
        } else {
            podmanBadge.className = 'badge-status inactive';
            podmanBadge.innerHTML = `<span class="status-dot"></span> Podman: Unavailable`;
        }

        // QEMU virtual machine status badge
        const qemuBadge = document.getElementById('status-qemu');
        if (data.isQemuAvailable) {
            qemuBadge.className = 'badge-status active';
            qemuBadge.innerHTML = `<span class="status-dot"></span> QEMU: Ready`;
        } else {
            qemuBadge.className = 'badge-status inactive';
            qemuBadge.innerHTML = `<span class="status-dot"></span> QEMU: Unavailable`;
        }
    } catch (err) {
        console.error('Failed to query status:', err);
    }
}

// 2. Fetch quick-start prompt presets from API
async function loadPresets() {
    try {
        const res = await fetch('/api/ai/presets');
        const presets = await res.json();
        const container = document.getElementById('presets-list');
        container.innerHTML = '';

        presets.forEach(preset => {
            const chip = document.createElement('div');
            chip.className = 'preset-chip';
            chip.innerHTML = `
                <div class="preset-chip-title">✨ ${escapeHtml(preset.title)}</div>
                <div class="preset-chip-desc">${escapeHtml(preset.description)}</div>
            `;
            chip.addEventListener('click', () => {
                document.getElementById('user-prompt').value = preset.prompt;
                document.getElementById('user-prompt').focus();
            });
            container.appendChild(chip);
        });
    } catch (err) {
        console.error('Failed to load presets:', err);
    }
}

// 3. Register user interface event listeners
function setupEventListeners() {
    // Generate recipe from prompt
    document.getElementById('btn-generate-recipe').addEventListener('click', generateRecipe);

    // Clear prompt textarea
    document.getElementById('btn-clear-prompt').addEventListener('click', () => {
        document.getElementById('user-prompt').value = '';
    });

    // Add package manually in the editor
    document.getElementById('btn-add-pkg').addEventListener('click', addPackageFromInput);
    document.getElementById('new-pkg-input').addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            addPackageFromInput();
        }
    });

    // Refine recipe via conversational AI
    document.getElementById('btn-refine').addEventListener('click', refineRecipe);

    // Start build process
    document.getElementById('btn-start-build').addEventListener('click', startBuild);

    // Cancel active build
    document.getElementById('btn-cancel-build').addEventListener('click', cancelBuild);

    // Settings modal triggers
    document.getElementById('btn-open-settings').addEventListener('click', openSettingsModal);
    document.getElementById('btn-close-settings').addEventListener('click', closeSettingsModal);
    document.getElementById('btn-save-settings').addEventListener('click', saveSettings);
    document.getElementById('btn-test-settings').addEventListener('click', testSettings);

    // Build history modal triggers
    document.getElementById('btn-open-history').addEventListener('click', openHistoryModal);
    document.getElementById('btn-close-history').addEventListener('click', closeHistoryModal);

    // QEMU virtualization test
    document.getElementById('btn-test-qemu').addEventListener('click', testInQemu);

    // Reset to start a new build
    document.getElementById('btn-new-build').addEventListener('click', () => {
        document.getElementById('step-result').classList.add('hidden');
        document.getElementById('step-build').classList.add('hidden');
        document.getElementById('step-recipe').classList.add('hidden');
        document.getElementById('step-prompt').classList.remove('hidden');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });
}

// 4. Request recipe generation from Gemini AI
async function generateRecipe() {
    const promptInput = document.getElementById('user-prompt');
    const prompt = promptInput.value.trim();

    if (!prompt) {
        alert('Please enter a prompt describing the Linux system you want to build.');
        promptInput.focus();
        return;
    }

    const btn = document.getElementById('btn-generate-recipe');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> Gemini AI is architecting your system...`;

    try {
        const res = await fetch('/api/ai/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt: prompt })
        });

        if (!res.ok) {
            const err = await res.json();
            throw new Error(err.detail || err.error || 'Generation failed');
        }

        currentRecipe = await res.json();
        renderRecipe(currentRecipe);

        // Move to recipe inspection step
        document.getElementById('step-recipe').classList.remove('hidden');
        document.getElementById('step-recipe').scrollIntoView({ behavior: 'smooth' });
    } catch (err) {
        alert('Recipe generation error: ' + err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<span>✨ Generate Recipe with Gemini AI</span>`;
    }
}

// 5. Conversational recipe refinement
async function refineRecipe() {
    const refineInput = document.getElementById('refine-prompt');
    const refinementText = refineInput.value.trim();

    if (!refinementText) {
        alert('Please describe what adjustments or packages you want to change.');
        refineInput.focus();
        return;
    }

    updateRecipeFromForm();

    const btn = document.getElementById('btn-refine');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> Updating...`;

    try {
        const res = await fetch('/api/ai/refine', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                currentRecipe: currentRecipe,
                refinementPrompt: refinementText
            })
        });

        if (!res.ok) {
            const err = await res.json();
            throw new Error(err.detail || err.error || 'Update failed');
        }

        currentRecipe = await res.json();
        renderRecipe(currentRecipe);
        refineInput.value = '';
    } catch (err) {
        alert('Recipe refinement error: ' + err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<span>⚡ Refine with AI</span>`;
    }
}

// 6. Populate form with recipe values
function renderRecipe(recipe) {
    document.getElementById('recipe-summary').textContent = recipe.summary || 'No rationale available.';
    document.getElementById('field-distribution').value = recipe.distribution || 'alpine';
    document.getElementById('field-isolabel').value = recipe.isoLabel || 'AI_LINUX';
    document.getElementById('field-hostname').value = recipe.hostname || 'ailinux';
    document.getElementById('field-desktop').value = recipe.desktopEnvironment || 'none';
    document.getElementById('field-rootpass').value = recipe.rootPassword || 'root';
    document.getElementById('field-username').value = recipe.defaultUsername || 'user';
    document.getElementById('field-userpass').value = recipe.defaultUserPassword || 'live';
    document.getElementById('field-kernel').value = recipe.kernelParameters || 'quiet console=tty0';

    renderPackagesTags(recipe.packages || []);
}

function renderPackagesTags(packages) {
    const container = document.getElementById('packages-tags-container');
    container.innerHTML = '';

    packages.forEach((pkg, index) => {
        const pill = document.createElement('span');
        pill.className = 'tag-pill';
        pill.innerHTML = `
            ${escapeHtml(pkg)}
            <span class="tag-remove" title="Remove package">&times;</span>
        `;
        pill.querySelector('.tag-remove').addEventListener('click', () => {
            currentRecipe.packages.splice(index, 1);
            renderPackagesTags(currentRecipe.packages);
        });
        container.appendChild(pill);
    });
}

function addPackageFromInput() {
    const input = document.getElementById('new-pkg-input');
    const val = input.value.trim().toLowerCase();
    if (val && currentRecipe) {
        if (!currentRecipe.packages.includes(val)) {
            currentRecipe.packages.push(val);
            renderPackagesTags(currentRecipe.packages);
        }
        input.value = '';
    }
}

function updateRecipeFromForm() {
    if (!currentRecipe) return;

    currentRecipe.distribution = document.getElementById('field-distribution').value;
    currentRecipe.isoLabel = document.getElementById('field-isolabel').value;
    currentRecipe.hostname = document.getElementById('field-hostname').value;
    currentRecipe.desktopEnvironment = document.getElementById('field-desktop').value;
    currentRecipe.rootPassword = document.getElementById('field-rootpass').value;
    currentRecipe.defaultUsername = document.getElementById('field-username').value;
    currentRecipe.defaultUserPassword = document.getElementById('field-userpass').value;
    currentRecipe.kernelParameters = document.getElementById('field-kernel').value;
}

// 7. Enqueue build job and switch to build center
async function startBuild() {
    updateRecipeFromForm();

    const btn = document.getElementById('btn-start-build');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> Initializing build...`;

    // Clear terminal console
    const terminalBody = document.getElementById('terminal-logs');
    terminalBody.innerHTML = '';

    try {
        const res = await fetch('/api/build', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(currentRecipe)
        });

        if (!res.ok) {
            const err = await res.json();
            throw new Error(err.detail || err.error || 'Failed to initiate build.');
        }

        const job = await res.json();
        currentJobId = job.id;

        // Transition to live build monitor
        document.getElementById('step-build').classList.remove('hidden');
        document.getElementById('step-result').classList.add('hidden');
        document.getElementById('btn-cancel-build').disabled = false;
        document.getElementById('step-build').scrollIntoView({ behavior: 'smooth' });

        updateProgressBar(job.progressPercent, job.statusMessage);

        // Connect SSE real-time stream
        connectBuildStream(currentJobId);
    } catch (err) {
        alert('Build launch error: ' + err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<span>🚀 Start ISO Build</span>`;
    }
}

// 8. Server-Sent Events (SSE) live streaming
function connectBuildStream(jobId) {
    if (eventSource) {
        eventSource.close();
    }

    eventSource = new EventSource(`/api/build/${jobId}/stream`);

    eventSource.onmessage = (e) => {
        try {
            const msg = JSON.parse(e.data);

            if (msg.type === 'log') {
                appendLogEntry(msg.data);
            } else if (msg.type === 'job') {
                updateProgressBar(msg.data.progress, msg.data.statusMessage);

                if (msg.data.status === 'Completed') {
                    eventSource.close();
                    handleBuildSuccess(msg.data);
                } else if (msg.data.status === 'Failed' || msg.data.status === 'Cancelled') {
                    eventSource.close();
                    document.getElementById('btn-cancel-build').disabled = true;
                }
            }
        } catch (err) {
            console.error('SSE parsing error:', err);
        }
    };

    eventSource.onerror = () => {
        console.warn('SSE connection closed.');
        eventSource.close();
    };
}

function appendLogEntry(log) {
    const terminalBody = document.getElementById('terminal-logs');
    const line = document.createElement('div');
    line.className = `log-entry log-${log.level}`;

    const date = new Date(log.timestamp);
    const timeStr = date.toTimeString().split(' ')[0];

    line.innerHTML = `<span class="log-time">[${timeStr}]</span> ${escapeHtml(log.message)}`;
    terminalBody.appendChild(line);

    // Auto-scroll to latest log
    terminalBody.scrollTop = terminalBody.scrollHeight;
}

function updateProgressBar(percent, message) {
    document.getElementById('progress-bar').style.width = `${percent}%`;
    document.getElementById('progress-percent-label').textContent = `${percent}%`;
    document.getElementById('build-status-msg').textContent = message || '';
}

// 9. Build completion handling
function handleBuildSuccess(jobData) {
    document.getElementById('btn-cancel-build').disabled = true;
    document.getElementById('step-result').classList.remove('hidden');
    document.getElementById('step-result').scrollIntoView({ behavior: 'smooth' });

    document.getElementById('result-iso-name').textContent = jobData.outputFileName || 'system.iso';
    document.getElementById('result-iso-size').textContent = formatBytes(jobData.outputSize);
    document.getElementById('result-iso-sha').textContent = jobData.sha256 || 'N/A';

    // Direct download trigger
    const btnDownload = document.getElementById('btn-download-iso');
    btnDownload.onclick = () => {
        window.location.href = `/api/build/${currentJobId}/download`;
    };
}

// 10. Cancel active build
async function cancelBuild() {
    if (!currentJobId) return;

    if (confirm('Are you sure you want to cancel the build operation?')) {
        try {
            await fetch(`/api/build/${currentJobId}/cancel`, { method: 'POST' });
            document.getElementById('btn-cancel-build').disabled = true;
        } catch (err) {
            alert('Cancellation error: ' + err.message);
        }
    }
}

// 11. Launch ISO in QEMU virtual machine
async function testInQemu() {
    if (!currentJobId) return;

    const btn = document.getElementById('btn-test-qemu');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> Launching QEMU...`;

    try {
        const res = await fetch(`/api/build/${currentJobId}/test-qemu`, { method: 'POST' });
        const data = await res.json();
        alert(data.message);
    } catch (err) {
        alert('QEMU invocation error: ' + err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<span>🖥️ Launch in QEMU VM</span>`;
    }
}

// 12. Settings Modal
async function openSettingsModal() {
    try {
        const res = await fetch('/api/config');
        const data = await res.json();

        document.getElementById('settings-api-key').value = '';
        document.getElementById('settings-api-key').placeholder = data.hasApiKey ? `Configured (${data.apiKeyMasked})` : 'Paste Gemini API Key';
        
        const modelInput = document.getElementById('settings-model');
        const dataList = document.getElementById('models-datalist');
        if (dataList) {
            dataList.innerHTML = '';
            data.availableModels.forEach(m => {
                const opt = document.createElement('option');
                opt.value = m;
                dataList.appendChild(opt);
            });
        }
        if (modelInput) {
            modelInput.value = data.model || 'gemini-flash-latest';
        }

        document.getElementById('modal-settings').classList.add('active');
    } catch (err) {
        alert('Failed to load settings: ' + err.message);
    }
}

function closeSettingsModal() {
    document.getElementById('modal-settings').classList.remove('active');
}

async function saveSettings() {
    const key = document.getElementById('settings-api-key').value.trim();
    const model = document.getElementById('settings-model').value;

    try {
        const res = await fetch('/api/config', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                apiKey: key.length > 0 ? key : null,
                model: model
            })
        });

        if (res.ok) {
            alert('Settings saved successfully!');
            closeSettingsModal();
            await checkStatus();
        } else {
            alert('Failed to save settings.');
        }
    } catch (err) {
        alert('Save error: ' + err.message);
    }
}

async function testSettings() {
    const key = document.getElementById('settings-api-key').value.trim();
    const model = document.getElementById('settings-model').value;
    const testStatus = document.getElementById('settings-test-status');

    testStatus.textContent = 'Testing connection to Gemini...';
    testStatus.style.color = '#9ca3af';

    try {
        const res = await fetch('/api/config/test', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                apiKey: key.length > 0 ? key : null,
                model: model
            })
        });

        const data = await res.json();
        testStatus.textContent = data.message;
        testStatus.style.color = data.success ? '#10b981' : '#ef4444';
    } catch (err) {
        testStatus.textContent = 'Connection error: ' + err.message;
        testStatus.style.color = '#ef4444';
    }
}

// 13. Build History Modal
async function openHistoryModal() {
    try {
        const res = await fetch('/api/builds');
        const builds = await res.json();
        const listContainer = document.getElementById('history-list');
        listContainer.innerHTML = '';

        if (builds.length === 0) {
            listContainer.innerHTML = '<div style="color: var(--text-muted); text-align: center; padding: 2rem;">No previous builds found.</div>';
        } else {
            builds.forEach(b => {
                const item = document.createElement('div');
                item.className = 'preset-chip';
                item.style.marginBottom = '0.75rem';
                item.style.display = 'flex';
                item.style.justifyContent = 'space-between';
                item.style.alignItems = 'center';

                const date = new Date(b.createdAt).toLocaleString();
                item.innerHTML = `
                    <div>
                        <div style="font-weight: 700; font-size: 1rem; color: #60a5fa;">${escapeHtml(b.recipe?.isoLabel || 'ISO')} (${b.recipe?.distribution || 'linux'})</div>
                        <div style="font-size: 0.8rem; color: var(--text-muted);">${date} | Status: <b>${b.status}</b> | Size: ${formatBytes(b.outputIsoSize)}</div>
                    </div>
                    <div style="display: flex; gap: 0.5rem;">
                        ${b.status === 'Completed' ? `<button class="btn-header" onclick="window.location.href='/api/build/${b.id}/download'">⬇️ Download</button>` : ''}
                        <button class="btn-header" style="color: #ef4444;" onclick="deleteBuildJob('${b.id}')">🗑️</button>
                    </div>
                `;
                listContainer.appendChild(item);
            });
        }

        document.getElementById('modal-history').classList.add('active');
    } catch (err) {
        alert('Failed to load history: ' + err.message);
    }
}

function closeHistoryModal() {
    document.getElementById('modal-history').classList.remove('active');
}

async function deleteBuildJob(jobId) {
    if (confirm('Delete this build job and its workspace artifacts from disk?')) {
        await fetch(`/api/build/${jobId}`, { method: 'DELETE' });
        await openHistoryModal();
    }
}

// Utility string escaping
function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

// Utility byte formatting
function formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}
