// Shared AJAX/HTMX error handling

/**
 * Show an error alert banner inside the main content area.
 * Also re-enables any buttons that were disabled by the request.
 */
function handleAjaxError(message) {
    var text = message || 'Có lỗi xảy ra, vui lòng thử lại.';
    var html = '<div id="js-error-alert" class="alert alert-danger alert-dismissible fade show mb-3" role="alert">' +
        '<i class="bi bi-x-circle-fill me-2"></i>' + text +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>' +
        '</div>';

    // Remove any previous JS error alert
    var prev = document.getElementById('js-error-alert');
    if (prev) prev.remove();

    // Prepend to <main> or fallback to body
    var container = document.querySelector('main[role="main"]') || document.body;
    container.insertAdjacentHTML('afterbegin', html);

    // Re-enable buttons disabled during the request
    document.querySelectorAll('button[disabled], input[type="submit"][disabled]').forEach(function (el) {
        el.disabled = false;
    });
}

// HTMX responseError — fires when HTMX receives a non-2xx/3xx response
// In Development this can happen when DeveloperExceptionPage returns 500.
// In Production the middleware redirects to /Home/Error so HTMX follows to 200.
document.addEventListener('htmx:responseError', function (evt) {
    var status = evt.detail.xhr ? evt.detail.xhr.status : 0;
    var message = 'Có lỗi xảy ra (HTTP ' + status + '), vui lòng thử lại.';

    // Try to parse JSON body for a structured message (from JSON endpoints)
    try {
        var json = JSON.parse(evt.detail.xhr.responseText);
        if (json && json.message) message = json.message;
    } catch (e) { /* not JSON */ }

    handleAjaxError(message);
});
