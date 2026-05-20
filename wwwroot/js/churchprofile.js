// wwwroot/js/churchProfile.js

function getLocation() {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(showPosition);
    } else {
        document.getElementById("addressInput").value = "Geolocation is not supported by this browser.";
    }
}

function showPosition(position) {
    const latitude = position.coords.latitude;
    const longitude = position.coords.longitude;

    fetch(`/Identity/Account/Manage/ChurchProfile?handler=GetAddress&latitude=${latitude}&longitude=${longitude}`)
        .then(response => response.text())
        .then(address => {
            document.getElementById("addressInput").value = address;
        })
        .catch(error => {
            document.getElementById("addressInput").value = "Error retrieving address: " + error;
            console.error('Error:', error);
        });
}

function markChurchStatus(currentStatus) {
    const newStatus = currentStatus === "Open" ? "Closed" : "Open";

    fetch(`/Identity/Account/Manage/ChurchProfile?handler=MarkChurchStatus&status=${newStatus}`)
        .then(response => response.json())
        .then(response => {
            const button = document.querySelector('.btn_markShutdown');
            button.textContent = `Mark Church as ${currentStatus === "Open" ? "Open" : "Closed"}`;
            button.classList.toggle('btn-danger');
            button.classList.toggle('btn-success');
        })
        .catch(error => {
            alert("Error: " + error);
            console.error('Error:', error);
        });
}
