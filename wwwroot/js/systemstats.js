// wwwroot/js/systemStats.js

async function fetchSystemStats(baseUrl, token) {
    try {
        const response = await fetch(`${baseUrl}/api/SystemStats`, {
            method: 'GET',
            headers: {
                'RequestVerificationToken': token
            }
        });
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        const data = await response.json();
        console.log('System Stats:', data); // Log the data for debugging

        const validatedData = validateAndFormatData(data);
        if (validatedData) {
            document.getElementById('cpuUsage').innerText = sanitize(validatedData.cpuUsage.toFixed(2)) + "%";
            document.getElementById('memoryUsage').innerText = sanitize(validatedData.memoryUsage.toFixed(2)) + " MB";
        } else {
            console.error('Invalid data format:', data);
        }
    } catch (error) {
        console.error('Error fetching system stats:', error);
    }
}

function validateAndFormatData(data) {
    if (typeof data.cpuUsage !== 'number' || typeof data.memoryUsage !== 'number' ||
        typeof data.databaseSize !== 'number' || typeof data.logDatabaseSize !== 'number' ||
        typeof data.whitelistCount !== 'number') {
        return null;
    }

    // Format data if necessary
    data.cpuUsage = parseFloat(data.cpuUsage.toFixed(2));
    data.memoryUsage = parseFloat(data.memoryUsage.toFixed(2));
    data.whitelistCount = parseInt(data.whitelistCount, 10);

    return data;
}

function sanitize(input) {
    const element = document.createElement('div');
    element.innerText = input;
    return element.innerHTML;
}

function initializeSystemStats(baseUrl, token) {
    // Fetch system stats every 5 seconds
    setInterval(() => fetchSystemStats(baseUrl, token), 3000);

    // Initial fetch
    fetchSystemStats(baseUrl, token);
}
