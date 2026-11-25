/*!
 * TXAEditor - trình soạn thảo Markdown + HTML tự viết cho InternshipPortal.
 * Không phụ thuộc thư viện. Gồm 3 phần:
 *   1) TXAMarkdown.render(md)  : chuyển Markdown (kèm HTML thô) sang HTML
 *   2) TXAMarkdown.sanitize(h) : lọc HTML theo danh sách cho phép (chặn script, on*, javascript:)
 *   3) TXAEditor               : khung soạn thảo gắn vào <textarea data-txa-editor>
 * Phím tắt dùng Alt + phím (không Ctrl) để không đụng phím tắt của trình duyệt và bộ gõ tiếng Việt.
 */
(function (global) {
    'use strict';

    /* =====================================================================
       1) Markdown -> HTML
       ===================================================================== */
    function esc(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    var HTML_BLOCK = /^\s*<\/?(div|p|table|thead|tbody|tr|td|th|ul|ol|li|h[1-6]|blockquote|pre|details|summary|section|article|hr|img|figure|br|center)\b/i;

    function renderInline(text) {
        var store = [];
        function keep(html) { store.push(html); return '\u0000' + (store.length - 1) + '\u0000'; }

        // Ký tự thoát: \* \_ \` ...
        text = text.replace(/\\([\\`*_{}\[\]()#+\-.!~=|>])/g, function (m, c) { return keep(esc(c)); });
        // Mã nội dòng
        text = text.replace(/(`+)([\s\S]*?[^`])\1(?!`)/g, function (m, t, code) { return keep('<code>' + esc(code.trim()) + '</code>'); });
        // Ảnh ![alt](url "title")
        text = text.replace(/!\[([^\]]*)\]\(\s*([^)\s]+)(?:\s+"([^"]*)")?\s*\)/g, function (m, alt, url, title) {
            return keep('<img src="' + esc(url) + '" alt="' + esc(alt) + '"' + (title ? ' title="' + esc(title) + '"' : '') + ' loading="lazy">');
        });
        // Liên kết [text](url "title")
        text = text.replace(/\[([^\]]+)\]\(\s*([^)\s]+)(?:\s+"([^"]*)")?\s*\)/g, function (m, label, url, title) {
            return keep('<a href="' + esc(url) + '"' + (title ? ' title="' + esc(title) + '"' : '') + '>') + label + keep('</a>');
        });
        // Liên kết tự động <https://...>
        text = text.replace(/<((?:https?:\/\/|mailto:)[^>\s]+)>/g, function (m, url) {
            return keep('<a href="' + esc(url) + '">' + esc(url) + '</a>');
        });
        // Định dạng chữ
        text = text
            .replace(/\*\*\*([^*\n]+?)\*\*\*/g, '<strong><em>$1</em></strong>')
            .replace(/\*\*([^*\n]+?)\*\*/g, '<strong>$1</strong>')
            .replace(/__([^_\n]+?)__/g, '<strong>$1</strong>')
            .replace(/(^|[^*\w])\*([^*\n]+?)\*(?!\w)/g, '$1<em>$2</em>')
            .replace(/(^|[^_\w])_([^_\n]+?)_(?!\w)/g, '$1<em>$2</em>')
            .replace(/~~([^~\n]+?)~~/g, '<del>$1</del>')
            .replace(/==([^=\n]+?)==/g, '<mark>$1</mark>')
            .replace(/\+\+([^+\n]+?)\+\+/g, '<u>$1</u>');

        return text.replace(/\u0000(\d+)\u0000/g, function (m, i) { return store[+i]; });
    }

    function splitRow(line) {
        var s = line.trim();
        if (s.charAt(0) === '|') s = s.slice(1);
        if (s.charAt(s.length - 1) === '|') s = s.slice(0, -1);
        return s.split(/(?<!\\)\|/).map(function (c) { return c.trim().replace(/\\\|/g, '|'); });
    }

    var LIST_RE = /^(\s*)([-*+]|\d+[.)])\s+(.*)$/;

    function renderList(lines) {
        // lines: các dòng của một khối danh sách; dựng cây theo độ thụt lề
        var html = '', stack = [];
        lines.forEach(function (raw) {
            var m = raw.match(LIST_RE);
            if (!m) {
                // Dòng nối tiếp của mục trước
                if (stack.length) html += '<br>' + renderInline(raw.trim());
                return;
            }
            var indent = m[1].replace(/\t/g, '    ').length;
            var ordered = /\d/.test(m[2]);
            var tag = ordered ? 'ol' : 'ul';
            while (stack.length && indent < stack[stack.length - 1].indent) {
                html += '</li></' + stack.pop().tag + '>';
            }
            var top = stack[stack.length - 1];
            // Cùng cấp nhưng đổi loại (chấm <-> số): đóng danh sách cũ, mở danh sách mới
            if (top && indent === top.indent && top.tag !== tag) {
                html += '</li></' + stack.pop().tag + '>';
                top = stack[stack.length - 1];
            }
            if (!top || indent > top.indent) {
                var start = ordered && parseInt(m[2], 10) !== 1 ? ' start="' + parseInt(m[2], 10) + '"' : '';
                html += '<' + tag + start + '>';
                stack.push({ indent: indent, tag: tag });
            } else {
                html += '</li>';
            }
            var body = m[3], task = body.match(/^\[( |x|X)\]\s+(.*)$/);
            if (task) {
                var done = task[1] !== ' ';
                html += '<li class="txa-task' + (done ? ' txa-done' : '') + '"><span class="txa-check">' + (done ? '&#9745;' : '&#9744;') + '</span> ' + renderInline(task[2]);
            } else {
                html += '<li>' + renderInline(body);
            }
        });
        while (stack.length) html += '</li></' + stack.pop().tag + '>';
        return html;
    }

    function renderBlocks(src) {
        var lines = src.replace(/\r\n?/g, '\n').split('\n');
        var out = [], i = 0;

        function isBlockStart(l) {
            return /^\s*(```|~~~)/.test(l) || /^#{1,6}\s/.test(l) || /^\s*>/.test(l) || LIST_RE.test(l) ||
                /^\s*([-*_])(\s*\1){2,}\s*$/.test(l) || HTML_BLOCK.test(l);
        }

        while (i < lines.length) {
            var line = lines[i];

            if (!line.trim()) { i++; continue; }

            // Khối mã ``` lang
            var fence = line.match(/^\s*(```|~~~)\s*([\w#+.-]*)\s*$/);
            if (fence) {
                var code = [];
                i++;
                while (i < lines.length && !new RegExp('^\\s*' + fence[1]).test(lines[i])) { code.push(lines[i]); i++; }
                i++;
                out.push('<pre><code' + (fence[2] ? ' class="language-' + esc(fence[2]) + '"' : '') + '>' + esc(code.join('\n')) + '</code></pre>');
                continue;
            }

            // Tiêu đề
            var h = line.match(/^(#{1,6})\s+(.*?)\s*#*\s*$/);
            if (h) { out.push('<h' + h[1].length + '>' + renderInline(h[2]) + '</h' + h[1].length + '>'); i++; continue; }

            // Đường kẻ ngang
            if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(line)) { out.push('<hr>'); i++; continue; }

            // Trích dẫn
            if (/^\s*>/.test(line)) {
                var q = [];
                while (i < lines.length && /^\s*>/.test(lines[i])) { q.push(lines[i].replace(/^\s*>\s?/, '')); i++; }
                out.push('<blockquote>' + renderBlocks(q.join('\n')) + '</blockquote>');
                continue;
            }

            // Bảng
            if (line.indexOf('|') > -1 && i + 1 < lines.length && /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(lines[i + 1])) {
                var head = splitRow(line), aligns = splitRow(lines[i + 1]).map(function (c) {
                    return /^:-+:$/.test(c) ? 'center' : /-+:$/.test(c) ? 'right' : /^:-+/.test(c) ? 'left' : '';
                });
                i += 2;
                var t = '<table><thead><tr>' + head.map(function (c, k) {
                    return '<th' + (aligns[k] ? ' align="' + aligns[k] + '"' : '') + '>' + renderInline(c) + '</th>';
                }).join('') + '</tr></thead><tbody>';
                while (i < lines.length && lines[i].indexOf('|') > -1 && lines[i].trim()) {
                    t += '<tr>' + splitRow(lines[i]).map(function (c, k) {
                        return '<td' + (aligns[k] ? ' align="' + aligns[k] + '"' : '') + '>' + renderInline(c) + '</td>';
                    }).join('') + '</tr>';
                    i++;
                }
                out.push(t + '</tbody></table>');
                continue;
            }

            // Danh sách
            if (LIST_RE.test(line)) {
                var items = [];
                while (i < lines.length && lines[i].trim() && (LIST_RE.test(lines[i]) || /^\s{2,}\S/.test(lines[i]))) { items.push(lines[i]); i++; }
                out.push(renderList(items));
                continue;
            }

            // Khối HTML thô: giữ nguyên tới dòng trống
            if (HTML_BLOCK.test(line)) {
                var block = [];
                while (i < lines.length && lines[i].trim()) { block.push(lines[i]); i++; }
                out.push(block.join('\n'));
                continue;
            }

            // Đoạn văn: xuống dòng đơn thành <br>
            var para = [];
            while (i < lines.length && lines[i].trim() && !(para.length && isBlockStart(lines[i]))) { para.push(lines[i].trim()); i++; }
            out.push('<p>' + para.map(renderInline).join('<br>') + '</p>');
        }
        return out.join('\n');
    }

    /* =====================================================================
       2) Lọc HTML an toàn (danh sách cho phép)
       ===================================================================== */
    var ALLOWED = {
        p: [], br: [], hr: [], div: ['align'], span: [], center: [],
        h1: [], h2: [], h3: [], h4: [], h5: [], h6: [],
        strong: [], b: [], em: [], i: [], u: [], s: [], del: [], ins: [], mark: [], small: [], sub: [], sup: [], kbd: [],
        code: [], pre: [], blockquote: [],
        ul: [], ol: ['start', 'type'], li: [],
        a: ['href', 'title'], img: ['src', 'alt', 'title', 'width', 'height', 'loading'],
        table: [], thead: [], tbody: [], tfoot: [], tr: [], th: ['align', 'colspan', 'rowspan'], td: ['align', 'colspan', 'rowspan'], caption: [],
        details: ['open'], summary: [], figure: [], figcaption: [], dl: [], dt: [], dd: []
    };
    var DROP_WITH_CONTENT = /^(script|style|iframe|frame|frameset|object|embed|applet|form|input|button|textarea|select|option|link|meta|base|svg|math|template|noscript|audio|video|source|track|canvas)$/i;
    var SAFE_STYLE = /^(color|background-color|text-align|font-weight|font-style|text-decoration|font-size)$/i;

    function safeUrl(url, isImg) {
        var u = String(url || '').trim();
        var compact = u.replace(/[\u0000- ]/g, '').toLowerCase();
        if (/^(https?:|mailto:|tel:|#|\/(?!\/))/.test(compact) || /^[\w\-./?=&%]+$/.test(compact) && compact.indexOf(':') === -1) return u;
        if (isImg && /^data:image\/(png|jpe?g|gif|webp);base64,/.test(compact)) return u;
        return null;
    }

    function cleanStyle(style) {
        return String(style || '').split(';').map(function (d) {
            var k = d.split(':')[0], v = d.slice(d.indexOf(':') + 1);
            if (!k || d.indexOf(':') < 0) return '';
            k = k.trim(); v = v.trim();
            if (!SAFE_STYLE.test(k) || /url\s*\(|expression|javascript:|[<>\\]/i.test(v)) return '';
            return k + ':' + v;
        }).filter(Boolean).join(';');
    }

    function sanitize(html) {
        var doc = new DOMParser().parseFromString('<body>' + html + '</body>', 'text/html');
        (function walk(node) {
            Array.prototype.slice.call(node.childNodes).forEach(function (el) {
                if (el.nodeType === 8) { el.remove(); return; }       // chú thích
                if (el.nodeType !== 1) return;                        // văn bản giữ nguyên
                var tag = el.tagName.toLowerCase();
                if (DROP_WITH_CONTENT.test(tag)) { el.remove(); return; }
                if (!ALLOWED.hasOwnProperty(tag)) {
                    walk(el);
                    while (el.firstChild) el.parentNode.insertBefore(el.firstChild, el);
                    el.remove();
                    return;
                }
                Array.prototype.slice.call(el.attributes).forEach(function (a) {
                    var name = a.name.toLowerCase(), val = a.value;
                    if (name === 'style') {
                        var st = cleanStyle(val);
                        st ? el.setAttribute('style', st) : el.removeAttribute('style');
                    } else if (name === 'class') {
                        var cls = val.split(/\s+/).filter(function (c) { return /^(language-[\w#+.-]+|txa-[\w-]+)$/.test(c); }).join(' ');
                        cls ? el.setAttribute('class', cls) : el.removeAttribute('class');
                    } else if (ALLOWED[tag].indexOf(name) < 0) {
                        el.removeAttribute(a.name);
                    } else if (name === 'href' || name === 'src') {
                        var ok = safeUrl(val, name === 'src');
                        ok === null ? el.removeAttribute(a.name) : el.setAttribute(a.name, ok);
                    }
                });
                if (tag === 'a') {
                    el.setAttribute('rel', 'noopener noreferrer nofollow');
                    if (/^https?:/i.test(el.getAttribute('href') || '')) el.setAttribute('target', '_blank');
                }
                if (tag === 'img' && !el.getAttribute('src')) { el.remove(); return; }
                walk(el);
            });
        })(doc.body);
        return doc.body.innerHTML;
    }

    var TXAMarkdown = {
        toHtml: function (md) { return renderBlocks(String(md || '')); },
        sanitize: sanitize,
        render: function (md) { return sanitize(renderBlocks(String(md || ''))); }
    };

    /* =====================================================================
       3) Khung soạn thảo
       ===================================================================== */
    var SHORTCUTS = [
        // [mã phím, lệnh, nhãn hiển thị]
        ['KeyB', 'bold', 'Alt + B'], ['KeyI', 'italic', 'Alt + I'], ['KeyU', 'underline', 'Alt + U'],
        ['KeyS', 'strike', 'Alt + S'], ['KeyE', 'mark', 'Alt + E'], ['KeyC', 'code', 'Alt + C'],
        ['KeyK', 'link', 'Alt + K'], ['KeyG', 'image', 'Alt + G'],
        ['Digit1', 'h1', 'Alt + 1'], ['Digit2', 'h2', 'Alt + 2'], ['Digit3', 'h3', 'Alt + 3'],
        ['KeyL', 'ul', 'Alt + L'], ['KeyO', 'ol', 'Alt + O'], ['KeyX', 'task', 'Alt + X'],
        ['KeyQ', 'quote', 'Alt + Q'], ['KeyR', 'codeblock', 'Alt + R'], ['KeyT', 'table', 'Alt + T'],
        ['KeyH', 'hr', 'Alt + H'], ['KeyP', 'preview', 'Alt + P'], ['KeyM', 'cycle', 'Alt + M'],
        ['KeyF', 'fullscreen', 'Alt + F'], ['Slash', 'help', 'Alt + /']
    ];
    var KEYMAP = {}, KEYMAP_BY_KEY = { '?': 'help' }, LABEL = {};
    SHORTCUTS.forEach(function (s) {
        KEYMAP[s[0]] = s[1];
        LABEL[s[1]] = s[2];
        // Dự phòng theo ký tự (KeyB -> "b", Digit2 -> "2", Slash -> "/") cho bàn phím / bộ gõ không gửi e.code chuẩn
        var k = s[0].replace(/^Key|^Digit/, '').toLowerCase();
        KEYMAP_BY_KEY[s[0] === 'Slash' ? '/' : k] = s[1];
    });

    var NAMES = {
        bold: 'In đậm', italic: 'In nghiêng', underline: 'Gạch chân', strike: 'Gạch ngang', mark: 'Tô sáng', code: 'Mã nội dòng',
        link: 'Chèn liên kết', image: 'Chèn ảnh (URL)', h1: 'Tiêu đề 1', h2: 'Tiêu đề 2', h3: 'Tiêu đề 3',
        ul: 'Danh sách chấm', ol: 'Danh sách số', task: 'Danh sách việc', quote: 'Trích dẫn', codeblock: 'Khối mã',
        table: 'Chèn bảng', hr: 'Đường kẻ ngang', preview: 'Bật / tắt xem trước', cycle: 'Đổi chế độ xem',
        fullscreen: 'Toàn màn hình', help: 'Phím tắt', undo: 'Hoàn tác (Ctrl + Z)', redo: 'Làm lại (Ctrl + Y)'
    };

    var ICONS = {
        bold: 'bi-type-bold', italic: 'bi-type-italic', underline: 'bi-type-underline', strike: 'bi-type-strikethrough',
        mark: 'bi-highlighter', code: 'bi-code', link: 'bi-link-45deg', image: 'bi-image', h1: 'bi-type-h1', h2: 'bi-type-h2',
        h3: 'bi-type-h3', ul: 'bi-list-ul', ol: 'bi-list-ol', task: 'bi-check2-square', quote: 'bi-quote',
        codeblock: 'bi-code-square', table: 'bi-table', hr: 'bi-hr', fullscreen: 'bi-arrows-fullscreen', help: 'bi-keyboard',
        undo: 'bi-arrow-counterclockwise', redo: 'bi-arrow-clockwise'
    };

    var TOOLBAR = [['undo', 'redo'], ['h1', 'h2', 'h3'], ['bold', 'italic', 'underline', 'strike', 'mark'],
        ['ul', 'ol', 'task', 'quote'], ['link', 'image', 'code', 'codeblock', 'table', 'hr']];
    var BUBBLE = ['bold', 'italic', 'underline', 'strike', 'mark', 'code', 'link', 'h2', 'quote', 'ul'];
    var MODES = [['edit', 'Soạn thảo', 'bi-pencil'], ['split', 'Chia đôi', 'bi-layout-split'], ['preview', 'Xem trước', 'bi-eye'], ['html', 'Mã HTML', 'bi-filetype-html']];

    function h(tag, attrs, html) {
        var el = document.createElement(tag);
        if (attrs) Object.keys(attrs).forEach(function (k) { el.setAttribute(k, attrs[k]); });
        if (html != null) el.innerHTML = html;
        return el;
    }

    function btn(cmd, extraClass) {
        var title = NAMES[cmd] + (LABEL[cmd] ? ' (' + LABEL[cmd] + ')' : '');
        return '<button type="button" class="txa-btn ' + (extraClass || '') + '" data-cmd="' + cmd + '" title="' + title + '" aria-label="' + title + '"><i class="bi ' + ICONS[cmd] + '"></i></button>';
    }

    function TXAEditor(textarea) {
        if (textarea.__txa) return textarea.__txa;
        textarea.__txa = this;
        this.ta = textarea;
        this.mode = window.matchMedia('(min-width: 992px)').matches ? 'split' : 'edit';
        this.build();
        this.bind();
        this.setMode(this.mode);
        this.update();
    }

    TXAEditor.prototype.build = function () {
        var ta = this.ta;
        var root = this.root = h('div', { 'class': 'txa' });
        ta.parentNode.insertBefore(root, ta);

        var toolbar = '<div class="txa-toolbar" role="toolbar" aria-label="Thanh công cụ soạn thảo">' +
            TOOLBAR.map(function (g) { return '<div class="txa-group">' + g.map(function (c) { return btn(c); }).join('') + '</div>'; }).join('') +
            '<div class="txa-spacer"></div><div class="txa-modes" role="tablist" aria-label="Chế độ xem">' +
            MODES.map(function (m) {
                return '<button type="button" class="txa-mode" role="tab" data-mode="' + m[0] + '" title="' + m[1] + '"><i class="bi ' + m[2] + '"></i><span>' + m[1] + '</span></button>';
            }).join('') + '</div>' +
            '<div class="txa-group">' + btn('fullscreen') + btn('help') + '</div></div>';
        root.insertAdjacentHTML('beforeend', toolbar);

        var body = this.body = h('div', { 'class': 'txa-body' });
        root.appendChild(body);
        ta.classList.add('txa-input');
        ta.setAttribute('spellcheck', 'false');
        body.appendChild(ta);
        this.preview = h('div', { 'class': 'txa-preview txa-content', 'aria-live': 'polite' });
        body.appendChild(this.preview);
        this.htmlPane = h('div', { 'class': 'txa-htmlpane' },
            '<button type="button" class="txa-copy btn btn-sm btn-outline-secondary"><i class="bi bi-clipboard me-1"></i>Sao chép HTML</button><pre><code></code></pre>');
        body.appendChild(this.htmlPane);

        // Thanh trạng thái: đếm chữ | gợi ý | bản quyền + đồng hồ (HH:mm:ss dd/MM/yy)
        this.status = h('div', { 'class': 'txa-status' },
            '<span class="txa-count"></span>' +
            '<span class="txa-hint"><i class="bi bi-markdown me-1"></i>Markdown + HTML · bôi đen chữ để định dạng nhanh · <kbd>Alt</kbd>+<kbd>/</kbd> xem phím tắt</span>' +
            '<span class="txa-brand"><span class="txa-copyright" title="TXAEditor - bản quyền thuộc TXA Studio">TXAEditor &copy; ' + new Date().getFullYear() + ' TXA Studio</span>' +
            '<time class="txa-clock" aria-hidden="true"></time></span>');
        root.appendChild(this.status);
        this.clock = this.status.querySelector('.txa-clock');
        this.tick();

        this.bubble = h('div', { 'class': 'txa-bubble', role: 'toolbar', 'aria-label': 'Định dạng nhanh' },
            BUBBLE.map(function (c) { return btn(c); }).join(''));
        root.appendChild(this.bubble);

        this.help = h('div', { 'class': 'txa-help', role: 'dialog', 'aria-label': 'Phím tắt TXAEditor' },
            '<div class="txa-help-head"><strong><i class="bi bi-keyboard me-1"></i>Phím tắt TXAEditor</strong>' +
            '<button type="button" class="btn-close" data-cmd="help" aria-label="Đóng"></button></div>' +
            '<p class="small text-secondary mb-2">Dùng phím <kbd>Alt</kbd> để không trùng phím tắt của trình duyệt và bộ gõ tiếng Việt.</p>' +
            '<div class="txa-help-grid">' + SHORTCUTS.map(function (s) {
                return '<span>' + NAMES[s[1]] + '</span><kbd>' + s[2] + '</kbd>';
            }).join('') + '</div>' +
            '<p class="small text-secondary mt-2 mb-0">Hỗ trợ Markdown (tiêu đề, danh sách, bảng, khối mã, trích dẫn) và HTML (đã lọc thẻ nguy hiểm).</p>');
        root.appendChild(this.help);
    };

    TXAEditor.prototype.bind = function () {
        var self = this, ta = this.ta, raf = 0;

        ta.addEventListener('input', function () {
            if (raf) return;
            raf = requestAnimationFrame(function () { raf = 0; self.update(); });
        });

        this.root.addEventListener('mousedown', function (e) {
            // Giữ vùng chọn trong textarea khi bấm nút
            if (e.target.closest('.txa-btn, .txa-mode')) e.preventDefault();
        });
        this.root.addEventListener('click', function (e) {
            var b = e.target.closest('[data-cmd]');
            if (b) { self.exec(b.getAttribute('data-cmd')); return; }
            var m = e.target.closest('[data-mode]');
            if (m) { self.setMode(m.getAttribute('data-mode')); return; }
            if (e.target.closest('.txa-copy')) self.copyHtml();
        });

        // Phím tắt bắt ở cả khung editor để vẫn dùng được khi đang ở chế độ Xem trước / HTML
        this.root.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (self.help.classList.contains('show')) { self.toggleHelp(false); e.preventDefault(); return; }
                if (self.root.classList.contains('txa-full')) { self.exec('fullscreen'); e.preventDefault(); return; }
                self.hideBubble();
            }
            var cmd = e.altKey && !e.ctrlKey && !e.metaKey ? (KEYMAP[e.code] || KEYMAP_BY_KEY[String(e.key).toLowerCase()]) : null;
            if (cmd) {
                e.preventDefault();
                e.stopPropagation();
                self.exec(cmd);
            }
        });

        ta.addEventListener('keydown', function (e) {
            // Enter trong danh sách: tự thêm gạch đầu dòng
            if (e.key === 'Enter' && !e.shiftKey && !e.altKey && !e.ctrlKey) self.continueList(e);
        });

        ['mouseup', 'keyup'].forEach(function (ev) {
            ta.addEventListener(ev, function () { setTimeout(function () { self.updateBubble(); }, 0); });
        });
        ta.addEventListener('blur', function () { setTimeout(function () { if (!self.root.contains(document.activeElement)) self.hideBubble(); }, 120); });
        ta.addEventListener('scroll', function () {
            self.hideBubble();
            if (self.mode === 'split') {
                var r = ta.scrollTop / Math.max(1, ta.scrollHeight - ta.clientHeight);
                self.preview.scrollTop = r * (self.preview.scrollHeight - self.preview.clientHeight);
            }
        });
    };

    TXAEditor.prototype.update = function () {
        var html = TXAMarkdown.render(this.ta.value);
        this.html = html;
        this.preview.innerHTML = html || '<p class="txa-empty">Nội dung xem trước sẽ hiện ở đây.</p>';
        if (this.mode === 'html') this.htmlPane.querySelector('code').textContent = html;
        var text = this.ta.value.trim();
        var words = text ? text.split(/\s+/).length : 0;
        this.status.querySelector('.txa-count').textContent = words + ' từ · ' + this.ta.value.length + ' ký tự';
    };

    function pad(n) { return n < 10 ? '0' + n : '' + n; }

    // Đồng hồ HH:mm:ss dd/MM/yy, tự dừng khi editor bị gỡ khỏi trang
    TXAEditor.prototype.tick = function () {
        var self = this;
        function draw() {
            if (!self.root.isConnected) { clearInterval(self.clockTimer); return; }
            var d = new Date();
            self.clock.textContent = pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds()) + ' ' +
                pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + pad(d.getFullYear() % 100);
        }
        draw();
        this.clockTimer = setInterval(draw, 1000);
    };

    TXAEditor.prototype.setMode = function (mode) {
        this.mode = mode;
        this.root.setAttribute('data-mode', mode);
        this.root.querySelectorAll('.txa-mode').forEach(function (b) {
            var on = b.getAttribute('data-mode') === mode;
            b.classList.toggle('active', on);
            b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        if (mode === 'html') this.htmlPane.querySelector('code').textContent = this.html || '';
        // Giữ focus trong editor để phím tắt Alt vẫn hoạt động ở mọi chế độ
        if (mode === 'edit' || mode === 'split') this.ta.focus({ preventScroll: true });
        else if (this.root.contains(document.activeElement)) this.root.querySelector('.txa-mode.active').focus({ preventScroll: true });
        this.hideBubble();
    };

    TXAEditor.prototype.copyHtml = function () {
        var b = this.htmlPane.querySelector('.txa-copy'), html = this.html || '';
        var done = function () { b.innerHTML = '<i class="bi bi-check2 me-1"></i>Đã sao chép'; setTimeout(function () { b.innerHTML = '<i class="bi bi-clipboard me-1"></i>Sao chép HTML'; }, 1600); };
        if (navigator.clipboard) navigator.clipboard.writeText(html).then(done, done); else done();
    };

    /* ---------- Thao tác văn bản (giữ được Ctrl+Z nhờ execCommand insertText) ---------- */
    TXAEditor.prototype.replace = function (start, end, text, selStart, selEnd) {
        var ta = this.ta;
        ta.focus();
        ta.setSelectionRange(start, end);
        var ok = false;
        try { ok = document.execCommand('insertText', false, text); } catch (e) { ok = false; }
        if (!ok || ta.value.substring(start, start + text.length) !== text) {
            ta.setRangeText(text, start, end, 'end');
            ta.dispatchEvent(new Event('input', { bubbles: true }));
        }
        ta.setSelectionRange(selStart == null ? start + text.length : selStart, selEnd == null ? (selStart == null ? start + text.length : selStart) : selEnd);
    };

    TXAEditor.prototype.wrap = function (before, after, placeholder) {
        var ta = this.ta, s = ta.selectionStart, e = ta.selectionEnd, v = ta.value;
        var sel = v.substring(s, e);

        // Chọn nhiều dòng / nhiều đoạn: bọc TỪNG dòng riêng (Markdown không cho định dạng chữ kéo qua nhiều đoạn)
        if (sel.indexOf('\n') > -1) {
            // Nội dung cũ đã bị bọc cả khối bởi một cặp dấu ngoài vùng chọn (vd *đoạn 1 ... đoạn 2*): gỡ cặp đó trước
            if (v.substring(s - before.length, s) === before && v.substring(e, e + after.length) === after) {
                this.replace(s - before.length, e + after.length, sel, s - before.length, e - before.length);
                return;
            }
            var LINE = /^(\s*(?:#{1,6}\s+|>\s?|[-*+]\s+(?:\[[ xX]\]\s+)?|\d+[.)]\s+)?)([\s\S]*?)(\s*)$/;
            var lines = sel.split('\n'), parts = lines.map(function (l) { return l.trim() ? LINE.exec(l) : null; });
            var filled = parts.filter(Boolean);
            var isWrapped = function (t) {
                return t.length > before.length + after.length && t.indexOf(before) === 0 && t.slice(-after.length) === after &&
                    !(before === '*' && t.indexOf('**') === 0);      // **đậm** không tính là đã in nghiêng
            };
            var all = filled.length > 0 && filled.every(function (m) { return isWrapped(m[2]); });
            var out = lines.map(function (l, i) {
                var m = parts[i];
                if (!m) return l;
                return all ? m[1] + m[2].slice(before.length, m[2].length - after.length) + m[3]
                           : m[1] + before + m[2] + after + m[3];
            }).join('\n');
            this.replace(s, e, out, s, s + out.length);
            return;
        }

        // Đã bọc sẵn thì gỡ (bật / tắt)
        if (v.substring(s - before.length, s) === before && v.substring(e, e + after.length) === after) {
            this.replace(s - before.length, e + after.length, sel, s - before.length, e - before.length);
            return;
        }
        if (sel.length > before.length + after.length && sel.indexOf(before) === 0 && sel.slice(-after.length) === after) {
            var inner = sel.slice(before.length, sel.length - after.length);
            this.replace(s, e, inner, s, s + inner.length);
            return;
        }
        var text = sel || placeholder;
        this.replace(s, e, before + text + after, s + before.length, s + before.length + text.length);
    };

    TXAEditor.prototype.lineRange = function () {
        var ta = this.ta, v = ta.value;
        var s = v.lastIndexOf('\n', ta.selectionStart - 1) + 1;
        var e = v.indexOf('\n', ta.selectionEnd);
        if (e < 0) e = v.length;
        return { s: s, e: e, text: v.substring(s, e) };
    };

    TXAEditor.prototype.prefixLines = function (make, test) {
        var r = this.lineRange(), lines = r.text.split('\n');
        // Chỉ coi là "đã định dạng" khi mọi dòng có chữ đều đã có tiền tố (dòng trống không tính)
        var filled = lines.filter(function (l) { return l.trim(); });
        var all = filled.length > 0 && filled.every(function (l) { return test.test(l); });
        var out = lines.map(function (l, i) {
            if (!l.trim() && lines.length > 1) return l;
            return all ? l.replace(test, '') : make(i) + l.replace(/^(#{1,6}\s+|>\s?|[-*+]\s+(\[[ xX]\]\s+)?|\d+[.)]\s+)/, '');
        }).join('\n');
        // Nhiều dòng: giữ vùng chọn để bấm tiếp; một dòng: đặt con trỏ cuối dòng để gõ tiếp
        if (lines.length > 1) this.replace(r.s, r.e, out, r.s, r.s + out.length);
        else this.replace(r.s, r.e, out);
    };

    TXAEditor.prototype.insertBlock = function (block, selectFrom, selectLen) {
        var ta = this.ta, s = ta.selectionStart, e = ta.selectionEnd, v = ta.value;
        var pre = s > 0 && v.charAt(s - 1) !== '\n' ? '\n\n' : (s > 1 && v.charAt(s - 2) !== '\n' && s > 0 ? '\n' : '');
        var post = v.charAt(e) && v.charAt(e) !== '\n' ? '\n\n' : '\n';
        var text = pre + block + post;
        var base = s + pre.length;
        this.replace(s, e, text, selectFrom == null ? s + text.length : base + selectFrom, selectFrom == null ? null : base + selectFrom + selectLen);
    };

    TXAEditor.prototype.continueList = function (e) {
        var ta = this.ta;
        if (ta.selectionStart !== ta.selectionEnd) return;
        var r = this.lineRange();
        var m = r.text.match(/^(\s*)([-*+]\s+\[[ xX]\]|[-*+]|\d+[.)])\s+(.*)$/);
        if (!m) return;
        e.preventDefault();
        if (!m[3].trim()) {                         // Mục rỗng: thoát danh sách
            this.replace(r.s, r.e, '', r.s);
            return;
        }
        var marker = m[2];
        var n = marker.match(/^(\d+)([.)])$/);
        if (n) marker = (parseInt(n[1], 10) + 1) + n[2];
        else if (/\[[ xX]\]/.test(marker)) marker = marker.charAt(0) + ' [ ]';
        var ins = '\n' + m[1] + marker + ' ';
        this.replace(ta.selectionStart, ta.selectionStart, ins);
    };

    TXAEditor.prototype.exec = function (cmd) {
        var ta = this.ta, sel = ta.value.substring(ta.selectionStart, ta.selectionEnd);
        switch (cmd) {
            case 'bold': this.wrap('**', '**', 'chữ đậm'); break;
            case 'italic': this.wrap('*', '*', 'chữ nghiêng'); break;
            case 'underline': this.wrap('++', '++', 'chữ gạch chân'); break;
            case 'strike': this.wrap('~~', '~~', 'chữ gạch ngang'); break;
            case 'mark': this.wrap('==', '==', 'chữ tô sáng'); break;
            case 'code': this.wrap('`', '`', 'mã'); break;
            case 'h1': case 'h2': case 'h3':
                var lv = +cmd.charAt(1), hashes = new Array(lv + 1).join('#') + ' ';
                this.prefixLines(function () { return hashes; }, new RegExp('^#{' + lv + '}\\s+'));
                break;
            case 'ul': this.prefixLines(function () { return '- '; }, /^\s*[-*+]\s+(?!\[[ xX]\])/); break;
            case 'ol': this.prefixLines(function (i) { return (i + 1) + '. '; }, /^\s*\d+[.)]\s+/); break;
            case 'task': this.prefixLines(function () { return '- [ ] '; }, /^\s*[-*+]\s+\[[ xX]\]\s+/); break;
            case 'quote': this.prefixLines(function () { return '> '; }, /^>\s?/); break;
            case 'link':
                var label = sel || 'văn bản liên kết', s1 = ta.selectionStart;
                this.replace(ta.selectionStart, ta.selectionEnd, '[' + label + '](https://)', s1 + label.length + 3, s1 + label.length + 11);
                break;
            case 'image':
                var alt = sel || 'mô tả ảnh', s2 = ta.selectionStart;
                this.replace(ta.selectionStart, ta.selectionEnd, '![' + alt + '](https://)', s2 + alt.length + 4, s2 + alt.length + 12);
                break;
            case 'codeblock':
                var body = sel || 'mã nguồn';
                this.insertBlock('```\n' + body + '\n```', 4, body.length);
                break;
            case 'table':
                this.insertBlock('| Cột 1 | Cột 2 | Cột 3 |\n| --- | --- | --- |\n| Nội dung | Nội dung | Nội dung |', 2, 5);
                break;
            case 'hr': this.insertBlock('---'); break;
            case 'undo': ta.focus(); try { document.execCommand('undo'); } catch (e) { } break;
            case 'redo': ta.focus(); try { document.execCommand('redo'); } catch (e) { } break;
            case 'preview': this.setMode(this.mode === 'preview' ? (window.innerWidth >= 992 ? 'split' : 'edit') : 'preview'); break;
            case 'cycle':
                var ids = MODES.map(function (m) { return m[0]; });
                this.setMode(ids[(ids.indexOf(this.mode) + 1) % ids.length]);
                break;
            case 'fullscreen':
                var full = this.root.classList.toggle('txa-full');
                document.body.classList.toggle('txa-noscroll', full);
                this.root.querySelector('[data-cmd="fullscreen"] i').className = 'bi ' + (full ? 'bi-fullscreen-exit' : 'bi-arrows-fullscreen');
                ta.focus();
                break;
            case 'help': this.toggleHelp(); break;
        }
        this.updateBubble();
    };

    TXAEditor.prototype.toggleHelp = function (force) {
        var show = force == null ? !this.help.classList.contains('show') : force;
        this.help.classList.toggle('show', show);
        if (!show) this.ta.focus();
    };

    /* ---------- Thanh định dạng nổi khi bôi đen chữ ---------- */
    TXAEditor.prototype.caretXY = function (pos) {
        var ta = this.ta, cs = getComputedStyle(ta), m = this._mirror;
        if (!m) {
            m = this._mirror = document.createElement('div');
            m.setAttribute('aria-hidden', 'true');
            document.body.appendChild(m);
        }
        ['boxSizing', 'width', 'fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'letterSpacing', 'paddingTop', 'paddingRight',
            'paddingBottom', 'paddingLeft', 'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth', 'tabSize', 'textIndent']
            .forEach(function (p) { m.style[p] = cs[p]; });
        m.style.position = 'absolute'; m.style.visibility = 'hidden'; m.style.top = '0'; m.style.left = '-9999px';
        m.style.whiteSpace = 'pre-wrap'; m.style.wordWrap = 'break-word'; m.style.overflow = 'hidden'; m.style.height = 'auto';
        m.textContent = ta.value.substring(0, pos);
        var span = document.createElement('span');
        span.textContent = ta.value.substring(pos) || '.';
        m.appendChild(span);
        return { x: span.offsetLeft, y: span.offsetTop - ta.scrollTop, lh: parseFloat(cs.lineHeight) || 20 };
    };

    TXAEditor.prototype.updateBubble = function () {
        var ta = this.ta;
        if (document.activeElement !== ta || ta.selectionStart === ta.selectionEnd || this.mode === 'preview' || this.mode === 'html') {
            this.hideBubble();
            return;
        }
        var p = this.caretXY(ta.selectionStart);
        var taRect = ta.getBoundingClientRect(), rootRect = this.root.getBoundingClientRect();
        var b = this.bubble;
        b.classList.add('show');
        var left = taRect.left - rootRect.left + p.x - b.offsetWidth / 2 + 20;
        var top = taRect.top - rootRect.top + p.y - b.offsetHeight - 8;
        // Ưu tiên đặt phía trên vùng chọn (kể cả đè lên thanh công cụ); chỉ khi sát mép trên khung mới đặt phía dưới dòng
        if (top < 0) top = taRect.top - rootRect.top + p.y + p.lh + 6;
        left = Math.max(4, Math.min(left, this.root.clientWidth - b.offsetWidth - 4));
        b.style.left = left + 'px';
        b.style.top = top + 'px';
    };

    TXAEditor.prototype.hideBubble = function () { this.bubble.classList.remove('show'); };

    /* ---------- Hiển thị nội dung đã lưu (trang chi tiết) ---------- */
    function renderStatic(root) {
        (root || document).querySelectorAll('[data-txa-render]').forEach(function (el) {
            if (el.__txaDone) return;
            el.__txaDone = true;
            var src = el.getAttribute('data-txa-render') || '';
            if (!src.trim()) return;
            el.innerHTML = TXAMarkdown.render(src);
            el.style.whiteSpace = 'normal';
            el.classList.add('txa-content');
        });
    }

    function init(root) {
        (root || document).querySelectorAll('textarea[data-txa-editor]').forEach(function (t) { new TXAEditor(t); });
        renderStatic(root);
    }

    global.TXAMarkdown = TXAMarkdown;
    global.TXAEditor = TXAEditor;
    global.TXAEditor.init = init;

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { init(); });
    else init();
})(window);
