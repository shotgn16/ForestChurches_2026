const repeatEventDropdown = document.getElementById('repeatEvent');
const frequencyLabel = document.getElementById('frequencyLabel');

repeatEventDropdown.addEventListener('change', () => {
    const value = repeatEventDropdown.value;
    frequencyLabel.textContent = `Repeats: ${value}`;
});

// Initialize the text equivalent on page load
repeatEventDropdown.dispatchEvent(new Event('change'));