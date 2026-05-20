function handleFormSubmission(index) {
    var isAuthenticated = document.getElementById('isAuthenticated').value === 'true';
    var idInput = document.getElementById('idValue_' + index);
    if (!isAuthenticated) {
        $('#emailModal').modal('show');
        document.getElementById('emailModalChurchId').value = idInput.value;
    } else {
        var form = document.createElement('form');
        form.setAttribute("method", "post");
        form.setAttribute("action", "/NearMe?handler=SendInformationWithEmail");
        var token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        var tokenInput = document.createElement("input");
        tokenInput.setAttribute("type", "hidden");
        tokenInput.setAttribute("name", "__RequestVerificationToken");
        tokenInput.setAttribute("value", token);
        form.appendChild(tokenInput);

        var fn = document.createElement("input");
        fn.setAttribute("type", "hidden");
        fn.setAttribute("name", "Id");
        fn.setAttribute("value", idInput.value);

        form.appendChild(fn);
        document.body.appendChild(form);

        form.submit();
    }
}
