function hideStatusMessage(sMessageElement) {
    // Set a timeout to hide the alert after 5 seconds (5000 milliseconds)
    setTimeout(function () {
        if (sMessageElement) {
            // Use Bootstrap's dismiss method to hide the alert
            var bsAlert = new bootstrap.Alert(sMessageElement);
            bsAlert.close();
        }
    }, 5000); // Change this value to adjust the duration the alert is visible
}

document.addEventListener("DOMContentLoaded", function () {
    var sMessageElement = document.getElementById("statusMessage");
    hideStatusMessage(sMessageElement);
});
