(function () {
    document.addEventListener('DOMContentLoaded', function () {
        var s = document.getElementById('flash-success')?.innerText;
        var e = document.getElementById('flash-error')?.innerText;
        if (s && window.Swal) Swal.fire({ icon: 'success', title: s, timer: 2500, showConfirmButton: false });
        if (e && window.Swal) Swal.fire({ icon: 'error', title: e });

        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (ev) {
                ev.preventDefault();
                Swal.fire({
                    title: form.getAttribute('data-confirm'),
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Yes',
                }).then(function (r) { if (r.isConfirmed) form.submit(); });
            });
        });
    });
})();
