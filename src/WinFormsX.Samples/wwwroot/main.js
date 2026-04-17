// WinFormsX WASM — DOM event bridge, requestAnimationFrame render loop,
// DPI scaling toolbar, and aspect-ratio-preserving canvas layout.

let dotnetExports = null;
let canvas = null;
let loopRunning = false;
let rafId = 0;

// ─── DPI / Scale State ──────────────────────────────────────────────
let currentScale = 1.0;
const BASE_WIDTH = 900;
const BASE_HEIGHT = 680;

/**
 * Fit the canvas into the container while preserving the aspect ratio.
 * The canvas CSS size is computed to be as large as possible within the
 * container while keeping the buffer aspect ratio.
 */
function fitCanvasToContainer() {
    if (!canvas) return;
    const container = document.getElementById('canvas-container');
    if (!container) return;

    const cw = container.clientWidth;
    const ch = container.clientHeight;
    const bufW = canvas.width;
    const bufH = canvas.height;
    const aspect = bufW / bufH;

    let cssW, cssH;
    if (cw / ch > aspect) {
        // container is wider than canvas aspect — height-limited
        cssH = ch;
        cssW = ch * aspect;
    } else {
        // container is taller than canvas aspect — width-limited
        cssW = cw;
        cssH = cw / aspect;
    }

    canvas.style.width = cssW + 'px';
    canvas.style.height = cssH + 'px';

    // Update info panel
    const infoCSS = document.getElementById('info-css');
    if (infoCSS) infoCSS.textContent = `${Math.round(cssW)} × ${Math.round(cssH)}`;
}

/**
 * Apply a new DPI scale factor. Resizes the canvas buffer and triggers
 * a .NET form resize + repaint.
 */
function applyScale(scale) {
    if (!canvas) return;
    currentScale = scale;

    const newW = Math.round(BASE_WIDTH * scale);
    const newH = Math.round(BASE_HEIGHT * scale);
    canvas.width = newW;
    canvas.height = newH;

    // Update info panel
    const infoBuf = document.getElementById('info-buffer');
    if (infoBuf) infoBuf.textContent = `${newW} × ${newH}`;

    // Update active button
    document.querySelectorAll('.dpi-btn').forEach(btn => {
        btn.classList.toggle('active', parseFloat(btn.dataset.scale) === scale);
    });

    fitCanvasToContainer();

    // Trigger a repaint at new resolution (OnFrame will pick up the new size)
    console.log(`[main.js] Scale changed to ${scale}× → buffer ${newW}×${newH}`);
}

// ─── Toolbar Setup ──────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.dpi-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const scale = parseFloat(btn.dataset.scale);
            if (!isNaN(scale)) applyScale(scale);
        });
    });
});

// ─── Resize Observer ────────────────────────────────────────────────
window.addEventListener('resize', fitCanvasToContainer);

/**
 * Initialize the JS-side event bridge.
 * Called from .NET via [JSImport("globalThis.__wfx_init")]
 */
globalThis.__wfx_init = function(width, height) {
    canvas = document.getElementById('winformsx-canvas');
    if (!canvas) {
        console.error('[main.js] Canvas element not found');
        return;
    }
    canvas.width = width;
    canvas.height = height;
    canvas.tabIndex = 0; // make focusable for keyboard events
    canvas.style.outline = 'none';

    // Initial fit
    fitCanvasToContainer();

    /**
     * Scale CSS pixel coordinates to canvas buffer coordinates.
     * Maps from CSS display size to internal buffer resolution.
     */
    function scaleCoords(e) {
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        return {
            x: (e.clientX - rect.left) * scaleX,
            y: (e.clientY - rect.top) * scaleY,
        };
    }

    // ─── Mouse Events ──────────────────────────────────────────────
    function getModifiers(e) {
        let m = 0;
        if (e.shiftKey) m |= 1;  // ModifierKeys.Shift
        if (e.ctrlKey)  m |= 2;  // ModifierKeys.Control
        if (e.altKey)   m |= 4;  // ModifierKeys.Alt
        return m;
    }

    canvas.addEventListener('mousedown', function(e) {
        e.preventDefault();
        canvas.focus();
        const { x, y } = scaleCoords(e);
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnMouseEvent(
                'mousedown', x, y, 0, e.button, getModifiers(e));
        } catch (err) { console.error('[main.js] mousedown error:', err); }
    });

    canvas.addEventListener('mouseup', function(e) {
        e.preventDefault();
        const { x, y } = scaleCoords(e);
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnMouseEvent(
                'mouseup', x, y, 0, e.button, getModifiers(e));
        } catch (err) { console.error('[main.js] mouseup error:', err); }
    });

    canvas.addEventListener('mousemove', function(e) {
        const { x, y } = scaleCoords(e);
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnMouseEvent(
                'mousemove', x, y, 0, 0, getModifiers(e));
        } catch (err) { /* throttle move errors */ }
    });

    canvas.addEventListener('wheel', function(e) {
        e.preventDefault();
        const { x, y } = scaleCoords(e);
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnMouseEvent(
                'wheel', x, y, -e.deltaY, 0, getModifiers(e));
        } catch (err) { console.error('[main.js] wheel error:', err); }
    }, { passive: false });

    canvas.addEventListener('dblclick', function(e) {
        e.preventDefault();
        const { x, y } = scaleCoords(e);
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnMouseEvent(
                'dblclick', x, y, 0, e.button, getModifiers(e));
        } catch (err) { console.error('[main.js] dblclick error:', err); }
    });

    canvas.addEventListener('contextmenu', function(e) { e.preventDefault(); });

    // ─── Keyboard Events ───────────────────────────────────────────
    canvas.addEventListener('keydown', function(e) {
        if (e.key === 'F5' || e.key === 'F12') return;
        e.preventDefault();
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnKeyEvent(
                'keydown', e.keyCode, e.key, getModifiers(e));
        } catch (err) { console.error('[main.js] keydown error:', err); }
    });

    canvas.addEventListener('keyup', function(e) {
        e.preventDefault();
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnKeyEvent(
                'keyup', e.keyCode, e.key, getModifiers(e));
        } catch (err) { console.error('[main.js] keyup error:', err); }
    });

    canvas.addEventListener('keypress', function(e) {
        e.preventDefault();
        try {
            dotnetExports.System.Drawing.WasmEventBridge.OnKeyEvent(
                'keypress', 0, e.key, getModifiers(e));
        } catch (err) { console.error('[main.js] keypress error:', err); }
    });

    // ─── Focus Events ──────────────────────────────────────────────
    canvas.addEventListener('focus', function() {
        try { dotnetExports.System.Drawing.WasmEventBridge.OnFocusEvent('focus'); }
        catch (err) { console.error('[main.js] focus error:', err); }
    });

    canvas.addEventListener('blur', function() {
        try { dotnetExports.System.Drawing.WasmEventBridge.OnFocusEvent('blur'); }
        catch (err) { console.error('[main.js] blur error:', err); }
    });

    console.log('[main.js] Event listeners initialized');
};

/**
 * Start the requestAnimationFrame render loop.
 */
globalThis.__wfx_startLoop = function() {
    if (loopRunning) return;
    loopRunning = true;
    console.log('[main.js] Render loop started');

    function frame() {
        if (!loopRunning) return;
        if (canvas && dotnetExports) {
            try {
                dotnetExports.System.Drawing.WasmEventBridge.OnFrame(
                    canvas.width, canvas.height);
            } catch (err) {
                console.error('[main.js] OnFrame error:', err);
            }
        }
        rafId = requestAnimationFrame(frame);
    }
    rafId = requestAnimationFrame(frame);
};

/**
 * Stop the render loop.
 */
globalThis.__wfx_stopLoop = function() {
    loopRunning = false;
    if (rafId) cancelAnimationFrame(rafId);
    console.log('[main.js] Render loop stopped');
};

/**
 * Called by index.html after the .NET runtime is created.
 */
export function setDotnetExports(exports) {
    dotnetExports = exports;
    console.log('[main.js] .NET exports registered');
}
