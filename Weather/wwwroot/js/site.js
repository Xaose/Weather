(() => {
    const searchForm = document.querySelector('.weather-search-form');
    if (!searchForm) {
        return;
    }

    const searchInput = searchForm.querySelector('[data-location-search]');
    const latitudeField = searchForm.querySelector('[data-location-latitude]');
    const longitudeField = searchForm.querySelector('[data-location-longitude]');
    const resultsContainer = searchForm.querySelector('[data-location-results]');
    const currentLocationButton = searchForm.querySelector('[data-current-location]');

    if (!searchInput || !latitudeField || !longitudeField || !resultsContainer) {
        return;
    }

    let searchTimeoutId = 0;
    let activeRequest = null;
    let isResolvingOnSubmit = false;

    const buildLocationLabel = (location) => [location.name, location.state, location.country]
        .filter(Boolean)
        .join(', ');

    const closeResults = () => {
        resultsContainer.innerHTML = '';
        resultsContainer.hidden = true;
    };

    const setSelectedLocation = (location) => {
        searchInput.value = buildLocationLabel(location);
        latitudeField.value = location.lat;
        longitudeField.value = location.lon;
    };

    const fetchLocations = async (query, limit) => {
        if (activeRequest) {
            activeRequest.abort();
        }

        activeRequest = new AbortController();

        const response = await fetch(`/api/locations/search?query=${encodeURIComponent(query)}&limit=${limit}`, {
            signal: activeRequest.signal
        });

        if (!response.ok) {
            throw new Error('Search request failed.');
        }

        return response.json();
    };

    const renderResults = (locations) => {
        if (!locations.length) {
            closeResults();
            return;
        }

        resultsContainer.innerHTML = locations
            .map((location) => {
                const label = buildLocationLabel(location);
                return `
                    <button
                        class="weather-search-result"
                        type="button"
                        data-location-option
                        data-location-label="${label}"
                        data-location-latitude="${location.lat}"
                        data-location-longitude="${location.lon}">
                        <span>${label}</span>
                        <small>${location.lat.toFixed(2)}, ${location.lon.toFixed(2)}</small>
                    </button>`;
            })
            .join('');

        resultsContainer.hidden = false;
    };

    const requestSearchResults = async (query, limit = 5) => {
        if (query.length < 2) {
            closeResults();
            return [];
        }

        try {
            const locations = await fetchLocations(query, limit);
            renderResults(locations);
            return locations;
        } catch (error) {
            if (error.name !== 'AbortError') {
                closeResults();
            }
            return [];
        }
    };

    searchInput.addEventListener('input', () => {
        window.clearTimeout(searchTimeoutId);
        latitudeField.value = '';
        longitudeField.value = '';

        const query = searchInput.value.trim();
        searchTimeoutId = window.setTimeout(() => {
            requestSearchResults(query);
        }, 220);
    });

    resultsContainer.addEventListener('click', (event) => {
        const option = event.target.closest('[data-location-option]');
        if (!option) {
            return;
        }

        searchInput.value = option.dataset.locationLabel || '';
        latitudeField.value = option.dataset.locationLatitude || '';
        longitudeField.value = option.dataset.locationLongitude || '';
        closeResults();
        searchForm.requestSubmit();
    });

    document.addEventListener('click', (event) => {
        if (!searchForm.contains(event.target)) {
            closeResults();
        }
    });

    searchForm.addEventListener('submit', async (event) => {
        if (latitudeField.value && longitudeField.value) {
            return;
        }

        const query = searchInput.value.trim();
        if (query.length < 2 || isResolvingOnSubmit) {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        isResolvingOnSubmit = true;

        const locations = await requestSearchResults(query, 1);
        if (locations.length) {
            setSelectedLocation(locations[0]);
            closeResults();
            searchForm.requestSubmit();
        }

        isResolvingOnSubmit = false;
    });

    currentLocationButton?.addEventListener('click', () => {
        if (!navigator.geolocation) {
            return;
        }

        navigator.geolocation.getCurrentPosition(async ({ coords }) => {
            latitudeField.value = String(coords.latitude);
            longitudeField.value = String(coords.longitude);

            try {
                const response = await fetch(`/api/locations/by-coordinates?latitude=${coords.latitude}&longitude=${coords.longitude}`);
                if (response.ok) {
                    const location = await response.json();
                    searchInput.value = buildLocationLabel(location);
                }
            } catch {
            }

            closeResults();
            searchForm.requestSubmit();
        });
    });
})();
