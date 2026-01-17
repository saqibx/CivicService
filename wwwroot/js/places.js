// Google Places Autocomplete initialization
let autocomplete;
let placesEnabled = false;

async function initPlacesAutocomplete() {
    const addressInput = document.getElementById('address');
    const locationStatus = document.getElementById('locationStatus');

    if (!addressInput) return;

    // Check if API key is configured
    const apiKey = window.APP_CONFIG?.GOOGLE_MAPS_API_KEY;
    if (!apiKey || apiKey === 'YOUR_GOOGLE_API_KEY') {
        console.log('Google Places API key not configured. Address autocomplete disabled.');
        showLocationStatus('info', 'Tip: Enter address manually, or click "Use My Current Location"');
        return;
    }

    try {
        // Load the Places library
        const { Autocomplete } = await google.maps.importLibrary("places");

        // Initialize autocomplete
        autocomplete = new Autocomplete(addressInput, {
            types: ['address'],
            fields: ['formatted_address', 'geometry', 'name']
        });

        placesEnabled = true;

        // Handle place selection
        autocomplete.addListener('place_changed', () => {
            const place = autocomplete.getPlace();

            if (!place.geometry || !place.geometry.location) {
                showLocationStatus('error', 'Please select an address from the dropdown.');
                clearCoordinates();
                return;
            }

            // Update hidden lat/lng fields
            const lat = place.geometry.location.lat();
            const lng = place.geometry.location.lng();

            document.getElementById('latitude').value = lat;
            document.getElementById('longitude').value = lng;

            // Update address field with formatted address
            addressInput.value = place.formatted_address || place.name;

            showLocationStatus('success', `Location captured: ${lat.toFixed(4)}, ${lng.toFixed(4)}`);
        });

    } catch (error) {
        console.warn('Google Places API not available:', error.message);
        placesEnabled = false;
        // Don't show error - just let user type manually
    }
}

function clearCoordinates() {
    document.getElementById('latitude').value = '';
    document.getElementById('longitude').value = '';
}

function showLocationStatus(type, message) {
    const locationStatus = document.getElementById('locationStatus');
    if (locationStatus) {
        locationStatus.className = `location-status ${type}`;
        locationStatus.textContent = message;
        locationStatus.style.display = 'block';
    }
}

function hideLocationStatus() {
    const locationStatus = document.getElementById('locationStatus');
    if (locationStatus) {
        locationStatus.className = 'location-status';
        locationStatus.textContent = '';
        locationStatus.style.display = 'none';
    }
}

// Enhanced geolocation with reverse geocoding
function enhanceGeolocation() {
    const useLocationBtn = document.getElementById('useLocation');
    if (!useLocationBtn) return;

    // Remove existing listener and add enhanced one
    useLocationBtn.replaceWith(useLocationBtn.cloneNode(true));
    const newBtn = document.getElementById('useLocation');

    newBtn.addEventListener('click', async () => {
        if (!navigator.geolocation) {
            showLocationStatus('error', 'Geolocation is not supported by your browser.');
            return;
        }

        newBtn.textContent = 'Getting location...';
        newBtn.disabled = true;

        navigator.geolocation.getCurrentPosition(
            async (position) => {
                const lat = position.coords.latitude;
                const lng = position.coords.longitude;

                document.getElementById('latitude').value = lat;
                document.getElementById('longitude').value = lng;

                // Try to reverse geocode to get address (only if API is available)
                const apiKey = window.APP_CONFIG?.GOOGLE_MAPS_API_KEY;
                if (apiKey && apiKey !== 'YOUR_GOOGLE_API_KEY') {
                    try {
                        const { Geocoder } = await google.maps.importLibrary("geocoding");
                        const geocoder = new Geocoder();

                        const response = await geocoder.geocode({
                            location: { lat, lng }
                        });

                        if (response.results[0]) {
                            document.getElementById('address').value = response.results[0].formatted_address;
                            showLocationStatus('success', `Location captured: ${lat.toFixed(4)}, ${lng.toFixed(4)}`);
                        }
                    } catch (e) {
                        // If geocoding fails, just use coordinates as address
                        document.getElementById('address').value = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
                        showLocationStatus('success', 'Coordinates captured. You can edit the address if needed.');
                    }
                } else {
                    // No API key - just use coordinates
                    document.getElementById('address').value = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
                    showLocationStatus('success', 'Coordinates captured. You can edit the address if needed.');
                }

                newBtn.textContent = 'Or Use My Current Location';
                newBtn.disabled = false;
            },
            (error) => {
                showLocationStatus('error', 'Unable to get location: ' + error.message);
                newBtn.textContent = 'Or Use My Current Location';
                newBtn.disabled = false;
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    });
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    // Check if API key is configured before trying to load Google Maps
    const apiKey = window.APP_CONFIG?.GOOGLE_MAPS_API_KEY;
    if (apiKey && apiKey !== 'YOUR_GOOGLE_API_KEY') {
        // Wait for Google Maps to load
        setTimeout(() => {
            initPlacesAutocomplete();
        }, 1000);
    } else {
        console.log('Google API key not configured - address autocomplete disabled');
    }

    // Always enable the geolocation button
    enhanceGeolocation();
});
