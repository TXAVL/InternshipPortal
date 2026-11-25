/*!
 * TXA UI - thay thế alert / confirm / tooltip mặc định của trình duyệt bằng giao diện riêng.
 *   TXAUI.confirm(msg | {title, message, okText, cancelText, variant})  -> Promise<boolean>
 *   TXAUI.alert(msg | {title, message, okText, variant})                -> Promise<void>
 *   TXAUI.toast(msg, {type: 'success'|'info'|'warning'|'danger', duration})
 *   HTML:  <form data-txa-confirm="Nội dung" data-txa-title="Tiêu đề" data-txa-ok="Đồng ý" data-txa-variant="danger">
 *          <a/button data-txa-confirm="...">           (liên kết và nút cũng dùng được)
 *   Tooltip: mọi phần tử có title="..." hoặc data-txa-tip="..." tự dùng tooltip TXA (title gốc bị gỡ để không hiện 2 lần)
 */
(function (global) {
    'use strict';

    var ICONS = { danger: 'bi-exclamation-octagon-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill', success: 'bi-check-circle-fill' };

    function el(tag, cls, html) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (html != null) e.innerHTML = html;
        return e;
    }
    function esc(s) { return String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    /* ======================= Hộp thoại ======================= */
    var queue = Promise.resolve();

    function dialog(opts) {
        // Xếp hàng để không chồng nhiều hộp thoại lên nhau
        var run = function () {
            return new Promise(function (resolve) {
                var variant = opts.variant || 'info';
                var previous = document.activeElement;
                var backdrop = el('div', 'txa-dlg-backdrop');
                var box = el('div', 'txa-dlg txa-dlg-' + variant);
                box.setAttribute('role', opts.cancel ? 'alertdialog' : 'dialog');
                box.setAttribute('aria-modal', 'true');
                var id = 'txa-dlg-' + Date.now();
                box.setAttribute('aria-labelledby', id + '-t');
                box.setAttribute('aria-describedby', id + '-m');
                box.innerHTML =
                    '<div class="txa-dlg-icon"><i class="bi ' + (ICONS[variant] || ICONS.info) + '"></i></div>' +
                    '<div class="txa-dlg-body"><h2 class="txa-dlg-title" id="' + id + '-t">' + esc(opts.title || 'Thông báo') + '</h2>' +
                    '<p class="txa-dlg-msg" id="' + id + '-m">' + esc(opts.message || '').replace(/\n/g, '<br>') + '</p></div>' +
                    '<div class="txa-dlg-actions"></div>';
                var actions = box.querySelector('.txa-dlg-actions');

                function close(result) {
                    document.removeEventListener('keydown', onKey, true);
                    box.classList.add('closing');
                    backdrop.classList.add('closing');
                    document.body.classList.remove('txa-dlg-open');
                    setTimeout(function () { backdrop.remove(); if (previous && previous.focus) previous.focus({ preventScroll: true }); resolve(result); }, 140);
                }

                var okBtn = el('button', 'btn ' + (variant === 'danger' ? 'btn-danger' : 'btn-primary'), esc(opts.okText || 'Đồng ý'));
                okBtn.type = 'button';
                okBtn.addEventListener('click', function () { close(true); });
                var cancelBtn = null;
                if (opts.cancel) {
                    cancelBtn = el('button', 'btn btn-outline-secondary', esc(opts.cancelText || 'Hủy'));
                    cancelBtn.type = 'button';
                    cancelBtn.addEventListener('click', function () { close(false); });
                    actions.appendChild(cancelBtn);
                }
                actions.appendChild(okBtn);

                function onKey(e) {
                    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(false); return; }
                    if (e.key === 'Tab') {                         // giữ focus trong hộp thoại
                        var f = box.querySelectorAll('button');
                        var first = f[0], last = f[f.length - 1];
                        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
                        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
                    }
                }
                document.addEventListener('keydown', onKey, true);
                backdrop.addEventListener('mousedown', function (e) { if (e.target === backdrop) close(false); });

                backdrop.appendChild(box);
                document.body.appendChild(backdrop);
                document.body.classList.add('txa-dlg-open');
                hideTip();
                // Với thao tác nguy hiểm, focus vào nút Hủy để Enter không xóa nhầm
                (variant === 'danger' && cancelBtn ? cancelBtn : okBtn).focus({ preventScroll: true });
            });
        };
        var p = queue.then(run, run);
        queue = p.then(function () { }, function () { });
        return p;
    }

    function norm(arg, defaults) {
        var o = typeof arg === 'string' ? { message: arg } : (arg || {});
        for (var k in defaults) if (o[k] == null) o[k] = defaults[k];
        return o;
    }

    /* ======================= Thông báo nổi ======================= */
    var toastHost;
    function toast(message, o) {
        o = o || {};
        var type = o.type || 'info';
        if (!toastHost) {
            toastHost = el('div', 'txa-toasts');
            toastHost.setAttribute('aria-live', 'polite');
            toastHost.setAttribute('role', 'status');
            document.body.appendChild(toastHost);
        }
        var t = el('div', 'txa-toast txa-toast-' + type,
            '<i class="bi ' + (ICONS[type] || ICONS.info) + '"></i><div class="txa-toast-msg">' + esc(message) + '</div>' +
            '<button type="button" class="txa-toast-x" aria-label="Đóng"><i class="bi bi-x-lg"></i></button>');
        toastHost.appendChild(t);
        function gone() { t.classList.add('closing'); setTimeout(function () { t.remove(); }, 180); }
        t.querySelector('.txa-toast-x').addEventListener('click', gone);
        var d = o.duration == null ? 4500 : o.duration;
        if (d > 0) setTimeout(gone, d);
        return gone;
    }

    /* ======================= Tooltip ======================= */
    var tip, tipFor = null, showTimer = 0;
    function ensureTip() {
        if (tip) return tip;
        tip = el('div', 'txa-tip');
        tip.id = 'txa-tip';
        tip.setAttribute('role', 'tooltip');
        document.body.appendChild(tip);
        return tip;
    }

    function tipText(target) {
        if (target.hasAttribute('title')) {
            var t = target.getAttribute('title');
            target.removeAttribute('title');              // gỡ để trình duyệt không hiện tooltip gốc
            if (t && t.trim()) {
                target.setAttribute('data-txa-tip', t);
                if (!target.hasAttribute('aria-label') && !target.textContent.trim()) target.setAttribute('aria-label', t);
            }
        }
        return target.getAttribute('data-txa-tip');
    }

    function place(target) {
        var r = target.getBoundingClientRect(), tr = tip.getBoundingClientRect();
        var gap = 10, vw = document.documentElement.clientWidth;
        var below = r.top - tr.height - gap < 4;
        var top = below ? r.bottom + gap : r.top - tr.height - gap;
        var left = r.left + r.width / 2 - tr.width / 2;
        var clamped = Math.max(6, Math.min(left, vw - tr.width - 6));
        tip.style.top = Math.round(top + window.scrollY) + 'px';
        tip.style.left = Math.round(clamped + window.scrollX) + 'px';
        tip.style.setProperty('--arrow-x', Math.round(r.left + r.width / 2 - clamped) + 'px');
        tip.classList.toggle('below', below);
    }

    function showTip(target) {
        var text = tipText(target);
        if (!text) return;
        ensureTip();
        tipFor = target;
        tip.textContent = text;
        tip.classList.remove('show');
        tip.style.top = '0px'; tip.style.left = '0px';
        place(target);
        tip.classList.add('show');
        target.setAttribute('aria-describedby', 'txa-tip');
    }

    function hideTip() {
        clearTimeout(showTimer);
        if (tip) tip.classList.remove('show');
        if (tipFor) { tipFor.removeAttribute('aria-describedby'); tipFor = null; }
    }

    function tipTarget(node) {
        var t = node && node.closest ? node.closest('[title],[data-txa-tip]') : null;
        if (!t || /^(IFRAME|SVG|HTML|BODY|ABBR)$/i.test(t.tagName)) return null;
        return t;
    }

    document.addEventListener('pointerover', function (e) {
        if (e.pointerType === 'touch') return;
        var t = tipTarget(e.target);
        if (!t || t === tipFor) return;
        // Gỡ title ngay để trình duyệt không kịp hiện tooltip mặc định
        tipText(t);
        clearTimeout(showTimer);
        showTimer = setTimeout(function () { showTip(t); }, 250);
    });
    document.addEventListener('pointerout', function (e) {
        var t = tipTarget(e.target);
        if (t && (!e.relatedTarget || !t.contains(e.relatedTarget))) hideTip();
    });
    document.addEventListener('focusin', function (e) {
        var t = tipTarget(e.target);
        if (t && t.matches(':focus-visible')) showTip(t);
    });
    document.addEventListener('focusout', hideTip);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') hideTip(); });
    document.addEventListener('scroll', hideTip, true);
    window.addEventListener('resize', hideTip);
    document.addEventListener('pointerdown', hideTip);

    /* ======================= data-txa-confirm ======================= */
    function confirmOpts(node) {
        return {
            title: node.getAttribute('data-txa-title') || 'Xác nhận',
            message: node.getAttribute('data-txa-confirm'),
            okText: node.getAttribute('data-txa-ok') || 'Đồng ý',
            cancelText: node.getAttribute('data-txa-cancel') || 'Hủy',
            variant: node.getAttribute('data-txa-variant') || 'warning',
            cancel: true
        };
    }

    document.addEventListener('submit', function (e) {
        var f = e.target;
        if (!f.hasAttribute || !f.hasAttribute('data-txa-confirm') || f.__txaOk) { f.__txaOk = false; return; }
        e.preventDefault();
        e.stopPropagation();      // các trình xử lý submit khác (kiểm tra form...) chỉ chạy sau khi người dùng đồng ý
        var submitter = e.submitter;
        dialog(confirmOpts(f)).then(function (ok) {
            if (!ok) return;
            f.__txaOk = true;
            if (f.requestSubmit) f.requestSubmit(submitter || undefined); else f.submit();
        });
    }, true);

    document.addEventListener('click', function (e) {
        var n = e.target.closest ? e.target.closest('a[data-txa-confirm], button[data-txa-confirm]') : null;
        if (!n || n.__txaOk) { if (n) n.__txaOk = false; return; }
        e.preventDefault();
        e.stopPropagation();
        dialog(confirmOpts(n)).then(function (ok) {
            if (!ok) return;
            n.__txaOk = true;
            n.click();
        });
    }, true);

    // alert() của trình duyệt -> thông báo TXA (bất đồng bộ)
    global.alert = function (msg) { return dialog(norm({ message: String(msg) }, { title: 'Thông báo', variant: 'info', cancel: false })); };

    global.TXAUI = {
        confirm: function (a) { return dialog(norm(a, { title: 'Xác nhận', variant: 'warning', cancel: true, okText: 'Đồng ý', cancelText: 'Hủy' })); },
        alert: function (a) { return dialog(norm(a, { title: 'Thông báo', variant: 'info', cancel: false })); },
        toast: toast,
        hideTip: hideTip
    };
})(window);
