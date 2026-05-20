document.addEventListener("DOMContentLoaded", function () {
    document.getElementById('createUserForm').addEventListener('submit', function () {
        var username = document.getElementById('usernameInput').value;
        this.action += '?username=' + encodeURIComponent(username);
    });
});
