/* Theme + pháo hoa. Lưu lựa chọn trên trình duyệt (localStorage); mặc định hệ thống do Admin đặt,
   được in sẵn vào <html data-default-theme data-default-fireworks>. */
(function () {
    'use strict';

    var KEY_T = 'ip-theme', KEY_F = 'ip-fireworks';
    var root = document.documentElement;
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    function get(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
    function set(k, v) { try { localStorage.setItem(k, v); } catch (e) { } }
    function del(k) { try { localStorage.removeItem(k); } catch (e) { } }

    var grid = document.getElementById('themeGrid');
    var toggle = document.getElementById('fireworksToggle');
    var note = document.getElementById('fireworksNote');
    var resetBtn = document.getElementById('themeReset');

    function darkOf(id) {
        var b = grid && grid.querySelector('[data-theme-id="' + id + '"]');
        return b ? b.dataset.dark === '1' : false;
    }

    function markActive(id) {
        if (!grid) return;
        grid.querySelectorAll('.theme-swatch').forEach(function (b) {
            var on = b.dataset.themeId === id;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    function applyTheme(id) {
        root.setAttribute('data-theme', id);
        root.setAttribute('data-bs-theme', darkOf(id) ? 'dark' : 'light');
        markActive(id);
    }

    /* ---------------- Pháo hoa ---------------- */
    var cv, ctx, rockets = [], parts = [], raf = 0, timer = 0, enabled = false;

    function ensureCanvas() {
        if (cv) return;
        cv = document.createElement('canvas');
        cv.className = 'fireworks-canvas';
        cv.setAttribute('aria-hidden', 'true');
        document.body.appendChild(cv);
        ctx = cv.getContext('2d');
        resize();
        window.addEventListener('resize', resize);
    }

    function resize() {
        var d = window.devicePixelRatio || 1;
        cv.width = innerWidth * d;
        cv.height = innerHeight * d;
        ctx.setTransform(d, 0, 0, d, 0, 0);
    }

    function palette() {
        if (root.getAttribute('data-theme') === 'tet') return ['#ff2d3f', '#ffd24a', '#ff8a3d', '#fff1b8'];
        var a = getComputedStyle(root).getPropertyValue('--accent-light').trim() || '#5eead4';
        return [a, '#ffd24a', '#ffffff', '#ff8fb1'];
    }

    function launch(x, ty) {
        rockets.push({ x: x, y: innerHeight, ty: ty, vy: -(9 + Math.random() * 3) });
        loop();
    }

    function explode(x, y) {
        var cols = palette(), n = 70 + Math.floor(Math.random() * 40), col = cols[Math.floor(Math.random() * cols.length)];
        for (var i = 0; i < n; i++) {
            var a = Math.random() * Math.PI * 2, s = 1 + Math.random() * 4.2;
            parts.push({
                x: x, y: y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: 1,
                decay: 0.011 + Math.random() * 0.012, c: Math.random() < 0.75 ? col : cols[Math.floor(Math.random() * cols.length)]
            });
        }
        if (parts.length > 900) parts.splice(0, parts.length - 900);
    }

    function loop() {
        if (raf) return;
        raf = requestAnimationFrame(frame);
    }

    function frame() {
        raf = 0;
        ctx.globalCompositeOperation = 'source-over';
        ctx.clearRect(0, 0, innerWidth, innerHeight);
        ctx.globalCompositeOperation = 'lighter';

        for (var i = rockets.length - 1; i >= 0; i--) {
            var r = rockets[i];
            r.y += r.vy; r.vy *= 0.985;
            ctx.fillStyle = '#ffe9a8';
            ctx.fillRect(r.x - 1, r.y, 2, 8);
            if (r.y <= r.ty || r.vy > -1.2) { explode(r.x, r.y); rockets.splice(i, 1); }
        }
        for (var j = parts.length - 1; j >= 0; j--) {
            var p = parts[j];
            p.x += p.vx; p.y += p.vy; p.vx *= 0.985; p.vy = p.vy * 0.985 + 0.045; p.life -= p.decay;
            if (p.life <= 0) { parts.splice(j, 1); continue; }
            ctx.globalAlpha = Math.max(p.life, 0);
            ctx.fillStyle = p.c;
            ctx.beginPath(); ctx.arc(p.x, p.y, 2, 0, 6.2832); ctx.fill();
        }
        ctx.globalAlpha = 1;

        if (rockets.length || parts.length) {
            loop();
        } else if (!enabled) {
            ctx.clearRect(0, 0, innerWidth, innerHeight);
        }
    }

    function schedule() {
        clearTimeout(timer);
        if (!enabled) return;
        timer = setTimeout(function () {
            if (enabled && !document.hidden) {
                launch(innerWidth * (0.12 + Math.random() * 0.76), innerHeight * (0.12 + Math.random() * 0.35));
            }
            schedule();
        }, 900 + Math.random() * 1400);
    }

    function setFireworks(on, save) {
        if (reduce) on = false;
        enabled = on;
        if (toggle) toggle.checked = on;
        if (save) set(KEY_F, on ? '1' : '0');
        if (on) { ensureCanvas(); schedule(); } else { clearTimeout(timer); }
    }

    document.addEventListener('click', function (e) {
        if (!enabled || e.target.closest('a,button,input,select,textarea,label,summary,.offcanvas,.modal,.dropdown-menu,.table-responsive')) return;
        launch(e.clientX, e.clientY);
    });

    /* ---------------- Khởi tạo ---------------- */
    var defTheme = root.getAttribute('data-default-theme') || 'ngoc';
    var defFw = root.getAttribute('data-default-fireworks') === 'true';

    applyTheme(get(KEY_T) || defTheme);
    var storedFw = get(KEY_F);
    setFireworks(storedFw === null ? defFw : storedFw === '1', false);

    if (reduce && toggle) {
        toggle.disabled = true;
        if (note) note.textContent = 'Pháo hoa đang tắt vì thiết bị của bạn bật chế độ giảm chuyển động.';
    }

    if (grid) {
        grid.addEventListener('click', function (e) {
            var b = e.target.closest('.theme-swatch');
            if (!b) return;
            var id = b.dataset.themeId;
            set(KEY_T, id);
            applyTheme(id);
            // Chọn theme Tết lần đầu thì bật pháo hoa cho đúng không khí
            if (id === 'tet' && get(KEY_F) === null) setFireworks(true, true);
        });
    }
    if (toggle) toggle.addEventListener('change', function () { setFireworks(toggle.checked, true); });
    if (resetBtn) resetBtn.addEventListener('click', function () {
        del(KEY_T); del(KEY_F);
        applyTheme(defTheme);
        setFireworks(defFw, false);
    });
})();
