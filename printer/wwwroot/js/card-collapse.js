// 卡片收合：自動為頁面上每個 .card 的 header 加上收合圖示按鈕（預設展開）。
// 已有手動 .collapse-toggle 的卡片（如事務機詳情）會跳過。
// 屬性控制：data-no-collapse = 不加收合鈕；data-collapsed = 預設收合。
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.card').forEach(function (card) {
        if (card.hasAttribute('data-no-collapse')) return;   // 標記不需收合的卡片
        var header = card.querySelector(':scope > .card-header');
        if (!header || header.querySelector('.collapse-toggle')) return;

        var startCollapsed = card.hasAttribute('data-collapsed');
        if (startCollapsed) card.classList.add('card-collapsed');

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-sm btn-light border collapse-toggle ms-2';
        btn.title = '展開/收合';
        btn.setAttribute('aria-expanded', startCollapsed ? 'false' : 'true');
        btn.innerHTML = '<i class="bi bi-chevron-down"></i>';
        btn.addEventListener('click', function () {
            var collapsed = card.classList.toggle('card-collapsed');
            btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        });

        header.classList.add('d-flex', 'align-items-center', 'justify-content-between');
        header.appendChild(btn);
    });
});
