document.addEventListener("DOMContentLoaded", function () {
    document.getElementById('createUserForm').addEventListener('submit', function (event) {
        var usernameInput = document.getElementById('usernameInput');
        if (usernameInput) {
            var username = usernameInput.value;
        } else {
            console.error('usernameInput element not found');
            event.preventDefault(); // Prevent form submission if the element is not found
        }
    });
});


function confirmDelete(roleName) {
    if (confirm("Are you sure you want to delete role '" + roleName + "'?")) {
        return true;
    } else {
        return false;
    }
}
