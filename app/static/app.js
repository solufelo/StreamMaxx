let destinationsData = {};

async function fetchStatus() {
  try {
    const res = await fetch('/api/status');
    if (!res.ok) return;
    const data = await res.json();
    updateDashboardHUD(data);
  } catch (err) {
    console.warn('Telemetry poll error:', err);
  }
}

async function fetchDestinations() {
  try {
    const res = await fetch('/api/destinations');
    if (!res.ok) return;
    destinationsData = await res.json();
    renderDestinations(destinationsData);
  } catch (err) {
    console.error('Destinations fetch error:', err);
  }
}

function updateDashboardHUD(data) {
  const dot = document.getElementById('masterStatusDot');
  const relayStatusText = document.getElementById('relayStatusText');
  const relaySubText = document.getElementById('relaySubText');
  const nvencBar = document.getElementById('nvencBar');
  const nvencVal = document.getElementById('nvencVal');
  const gpuTemp = document.getElementById('gpuTemp');
  const gpuVram = document.getElementById('gpuVram');
  const obsStatus = document.getElementById('obsStatus');
  const obsSub = document.getElementById('obsSub');

  // Relay status
  if (data.relay && data.relay.running) {
    const tel = data.relay.telemetry || {};
    if (tel.online) {
      dot.className = 'glow-indicator live';
      relayStatusText.textContent = 'BROADCASTING LIVE';
      relayStatusText.style.color = 'var(--neon-green)';
      relaySubText.textContent = `Forwarding to ${tel.readers || 0} active platforms`;
    } else {
      dot.className = 'glow-indicator standby';
      relayStatusText.textContent = 'RELAY STANDBY (1935)';
      relayStatusText.style.color = 'var(--neon-cyan)';
      relaySubText.textContent = 'Awaiting OBS master stream...';
    }
  } else {
    dot.className = 'glow-indicator';
    relayStatusText.textContent = 'RELAY OFFLINE';
    relayStatusText.style.color = 'var(--text-muted)';
    relaySubText.textContent = 'Click "Sync & Start Relay"';
  }

  // NVENC Telemetry
  const gpu = data.hardware ? data.hardware.telemetry : null;
  if (gpu) {
    const nvenc = gpu.nvenc_util || 0;
    nvencBar.style.width = nvenc + '%';
    nvencVal.textContent = nvenc + '%';
    gpuTemp.textContent = (gpu.temperature || 0) + ' ?C';
    gpuVram.textContent = `${gpu.vram_used || 0} / ${gpu.vram_total || 16380} MB`;
  }

  // OBS Status
  if (data.obs && data.obs.running) {
    obsStatus.textContent = 'OBS ACTIVE';
    obsStatus.style.color = 'var(--neon-green)';
    obsSub.textContent = 'Cores 6-7 [0xF000 Isolated]';
  } else {
    obsStatus.textContent = 'OBS IDLE';
    obsStatus.style.color = 'var(--text-muted)';
    obsSub.textContent = 'Ready to launch';
  }
}

function renderDestinations(data) {
  const container = document.getElementById('platformGrid');
  if (!container || !data.destinations) return;

  container.innerHTML = '';
  let activeCount = 0;

  for (const [id, dest] of Object.entries(data.destinations)) {
    if (dest.enabled) activeCount++;

    const card = document.createElement('div');
    card.className = `platform-card ${dest.enabled ? 'active' : ''}`;
    card.id = `card-${id}`;

    card.innerHTML = `
      <div class="platform-header">
        <div class="platform-title-group">
          <span class="platform-pill" style="background: ${dest.color || '#00f0ff'}"></span>
          <span class="platform-name">${dest.name}</span>
        </div>
        <label class="switch">
          <input type="checkbox" id="toggle-${id}" ${dest.enabled ? 'checked' : ''}>
          <span class="slider"></span>
        </label>
      </div>

      <div class="input-row">
        <span class="input-label">INGEST SERVER:</span>
        <input type="text" class="input-field" id="server-${id}" value="${dest.server || ''}" placeholder="rtmp://...">
      </div>

      <div class="input-row">
        <span class="input-label">STREAM KEY:</span>
        <div class="input-wrapper">
          <input type="password" class="input-field" id="key-${id}" value="${dest.stream_key || ''}" placeholder="Paste stream key here...">
          <button class="btn-small" id="btnShow-${id}">SHOW</button>
        </div>
      </div>

      <div class="platform-notes">
        ?? <strong>Target:</strong> ${dest.recommended_resolution || '1080p60'} @ ${dest.max_bitrate_kbps || 8000} Kbps.<br>
        <em>${dest.notes || ''}</em>
      </div>
    `;

    container.appendChild(card);

    // Toggle event
    const toggle = card.querySelector(`#toggle-${id}`);
    toggle.addEventListener('change', (e) => {
      dest.enabled = e.target.checked;
      if (e.target.checked) {
        card.classList.add('active');
      } else {
        card.classList.remove('active');
      }
      updateActiveCount();
      saveDestinationsDebounced();
    });

    // Inputs event
    const serverInput = card.querySelector(`#server-${id}`);
    serverInput.addEventListener('change', (e) => {
      dest.server = e.target.value.trim();
      saveDestinationsDebounced();
    });

    const keyInput = card.querySelector(`#key-${id}`);
    keyInput.addEventListener('change', (e) => {
      dest.stream_key = e.target.value.trim();
      saveDestinationsDebounced();
    });

    // Show/Hide toggle
    const btnShow = card.querySelector(`#btnShow-${id}`);
    btnShow.addEventListener('click', () => {
      if (keyInput.type === 'password') {
        keyInput.type = 'text';
        btnShow.textContent = 'HIDE';
      } else {
        keyInput.type = 'password';
        btnShow.textContent = 'SHOW';
      }
    });
  }

  updateActiveCount();
}

function updateActiveCount() {
  if (!destinationsData.destinations) return;
  let count = 0;
  for (const d of Object.values(destinationsData.destinations)) {
    if (d.enabled) count++;
  }
  const badge = document.getElementById('activeDestCount');
  if (badge) badge.textContent = `${count} ACTIVE`;
}

let saveTimeout = null;
function saveDestinationsDebounced() {
  clearTimeout(saveTimeout);
  saveTimeout = setTimeout(async () => {
    try {
      const res = await fetch('/api/destinations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(destinationsData)
      });
      const data = await res.json();
      if (data.success) {
        showToast('Settings saved & relay synced.');
      }
    } catch (err) {
      showToast('Error saving settings: ' + err.message, true);
    }
  }, 400);
}

function showToast(msg, isError = false) {
  const toast = document.getElementById('toast');
  if (!toast) return;
  toast.textContent = msg;
  toast.style.borderColor = isError ? 'var(--neon-red)' : 'var(--neon-cyan)';
  toast.classList.remove('hidden');
  setTimeout(() => {
    toast.classList.add('hidden');
  }, 3000);
}

// Button Bindings
document.addEventListener('DOMContentLoaded', () => {
  fetchDestinations();
  fetchStatus();
  setInterval(fetchStatus, 2000);

  document.getElementById('btnLaunchObs').addEventListener('click', async () => {
    showToast('Launching Optimaxxed OBS Studio...');
    try {
      const res = await fetch('/api/obs/launch', { method: 'POST' });
      const data = await res.json();
      showToast(data.message || 'OBS launch command dispatched.');
    } catch (err) {
      showToast('Failed to launch OBS: ' + err.message, true);
    }
  });

  document.getElementById('btnRestartRelay').addEventListener('click', async () => {
    showToast('Restarting MediaMTX Relay with updated endpoints...');
    try {
      const res = await fetch('/api/relay/start', { method: 'POST' });
      const data = await res.json();
      showToast(data.message || 'Relay restarted.');
    } catch (err) {
      showToast('Failed to restart relay: ' + err.message, true);
    }
  });

  document.getElementById('btnApplyQos').addEventListener('click', async () => {
    showToast('Re-applying DSCP 46 QoS & Ryzen Affinity...');
    try {
      const res = await fetch('/api/optimize', { method: 'POST' });
      const data = await res.json();
      showToast(data.message || 'QoS & Affinity updated.');
    } catch (err) {
      showToast('Failed to apply QoS: ' + err.message, true);
    }
  });

  document.getElementById('btnCopyIngest').addEventListener('click', () => {
    navigator.clipboard.writeText('rtmp://127.0.0.1:1935/live/master').then(() => {
      showToast('Copied: rtmp://127.0.0.1:1935/live/master');
    });
  });
});
