// 12. Settings Modal
async function openSettingsModal() {
    try {
        const res = await fetch('/api/config');
        const data = await res.json();

        document.getElementById('settings-api-key').value = '';
        document.getElementById('settings-api-key').placeholder = data.hasApiKey ? `Configured (${data.apiKeyMasked})` : 'Paste Gemini API Key';
        
        const modelInput = document.getElementById('settings-model');
        const dataList = document.getElementById('models-datalist');
        dataList.innerHTML = '';
        
        data.availableModels.forEach(m => {
            const opt = document.createElement('option');
            opt.value = m;
            dataList.appendChild(opt);
        });

        modelInput.value = data.model || 'gemini-flash-latest';

        document.getElementById('modal-settings').classList.add('active');
    } catch (err) {
        alert('Failed to load settings: ' + err.message);
    }
}
