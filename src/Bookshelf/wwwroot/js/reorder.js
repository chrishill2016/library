// Drag-to-reorder for the reading list: press a row's [data-drag-handle] and drag it up or down.
// Rows are the list's direct children with data-index. On drop, calls dotnet OnReorder(from, to).
// Pointer events work for touch and mouse alike (HTML5 drag-and-drop doesn't on Android).

export function attach(list, dotnetRef) {
    if (list.dataset.reorderAttached) return;
    list.dataset.reorderAttached = 'true';
    let drag = null;

    list.addEventListener('pointerdown', e => {
        const handle = e.target.closest('[data-drag-handle]');
        if (!handle || !list.contains(handle) || e.button > 0) return;
        const rows = [...list.children].filter(r => r.dataset.index !== undefined);
        const row = handle.closest('[data-index]');
        const from = rows.indexOf(row);
        if (from < 0) return;
        const rects = rows.map(r => r.getBoundingClientRect());
        const step = rows.length > 1 ? rects[1].top - rects[0].top : rects[0].height;
        drag = { handle, row, rows, rects, step, from, to: from, startY: e.clientY, pointerId: e.pointerId };
        handle.setPointerCapture(e.pointerId);
        row.classList.add('dragging');
        e.preventDefault();
    });

    list.addEventListener('pointermove', e => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const { rows, rects, row, from, step } = drag;
        const dy = e.clientY - drag.startY;
        row.style.transform = `translateY(${dy}px)`;
        const centre = rects[from].top + rects[from].height / 2 + dy;
        // New position = how many other rows have their middle above the dragged row's middle.
        let to = 0;
        rows.forEach((r, i) => { if (i !== from && rects[i].top + rects[i].height / 2 < centre) to++; });
        drag.to = to;
        rows.forEach((r, i) => {
            if (i === from) return;
            const shift = from < to && i > from && i <= to ? -step : to < from && i >= to && i < from ? step : 0;
            r.style.transform = shift ? `translateY(${shift}px)` : '';
        });
    });

    const finish = async e => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const { rows, row, from, to } = drag;
        drag = null;
        row.classList.remove('dragging');
        rows.forEach(r => { r.style.transform = ''; });
        if (e.type === 'pointerup' && to !== from) {
            await dotnetRef.invokeMethodAsync('OnReorder', from, to);
        }
    };
    list.addEventListener('pointerup', finish);
    list.addEventListener('pointercancel', finish);
}
