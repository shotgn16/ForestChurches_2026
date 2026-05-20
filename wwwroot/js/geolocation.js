const addressInput = document.getElementById("addressInput");
const addressInput2 = document.getElementById("addressInput2");

function getLocation() {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(showPosition);
    } else {
        addressInput.value = "Geolocation is not supported by this browser.";
    }
}

function showPosition(position) {
    const latitude = position.coords.latitude;
    const longitude = position.coords.longitude;

    $.ajax({
        url: `/NearMe?handler=GetAddress&latitude=${latitude}&longitude=${longitude}`,
        method: 'GET',
        success: function (address) {
            addressInput.value = address;
            addressInput2.value = address;
        },
        error: function (xhr, status, error) {
            addressInput.value = "Error retrieving address " + error;
            addressInput2.value = "Error retrieving address" + error;
            console.error('Error:', error);
        }
    });
}
