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

(function () {
    const root = document.querySelector('[data-weather-chat]');
    if (!root) {
        return;
    }

    const assistantEndpoint = root.dataset.assistantEndpoint || '/api/assistant/chat';
    const sessionList = root.querySelector('[data-chat-session-list]');
    const messageList = root.querySelector('[data-chat-messages]');
    const chatForm = root.querySelector('[data-chat-form]');
    const chatInput = root.querySelector('[data-chat-input]');
    const createSessionButton = root.querySelector('[data-chat-new-session]');
    const sessionTitle = root.querySelector('[data-chat-session-title]');
    const storageKey = 'weather-chat-sessions';
    const activeKey = 'weather-chat-active-session';

    const escapeHtml = (value) => String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');

    const isPlainObject = (value) => Boolean(value) && typeof value === 'object' && !Array.isArray(value);
    const formatTime = (value) => value
        ? new Date(value).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
        : '';
    const formatDate = (value) => value
        ? new Date(value).toLocaleDateString([], { month: 'short', day: 'numeric' })
        : '';
    const formatTemperature = (value) => {
        if (typeof value !== 'number' || Number.isNaN(value)) {
            return '—';
        }

        return `${Math.round(value)}°`;
    };
    const formatNumber = (value, suffix = '') => {
        if (typeof value !== 'number' || Number.isNaN(value)) {
            return '—';
        }

        return `${Math.round(value * 10) / 10}${suffix}`;
    };
    const formatPercent = (value) => typeof value === 'number' && !Number.isNaN(value) ? `${Math.round(value)}%` : '—';
    const toCelsius = (value) => {
        if (typeof value !== 'number' || Number.isNaN(value)) {
            return null;
        }

        return value > 170 ? value - 273.15 : value;
    };
    const toKmh = (value) => typeof value === 'number' && !Number.isNaN(value) ? value * 3.6 : null;
    const createId = () => window.crypto?.randomUUID?.() || `chat-${Date.now()}-${Math.random().toString(16).slice(2)}`;

    const loadSessions = () => {
        try {
            const parsed = JSON.parse(localStorage.getItem(storageKey) || '[]');
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    };

    const createSession = () => ({
        id: createId(),
        assistantSessionId: null,
        title: 'New chat',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        messages: []
    });

    let sessions = loadSessions();
    if (!sessions.length) {
        sessions = [createSession()];
    }

    let activeSessionId = localStorage.getItem(activeKey) || sessions[0].id;
    let isSending = false;
    let pendingTypingId = null;

    const persist = () => {
        localStorage.setItem(storageKey, JSON.stringify(sessions));
        localStorage.setItem(activeKey, activeSessionId);
    };

    const getActiveSession = () => {
        const existingSession = sessions.find((session) => session.id === activeSessionId);
        if (existingSession) {
            return existingSession;
        }

        activeSessionId = sessions[0].id;
        return sessions[0];
    };

    const setActiveSession = (id) => {
        activeSessionId = id;
        persist();
        render();
    };

    const upsertSession = (session) => {
        const index = sessions.findIndex((item) => item.id === session.id);
        if (index === -1) {
            sessions.unshift(session);
        } else {
            sessions[index] = session;
        }
    };

    const getWeatherSource = (data) => {
        if (!isPlainObject(data)) {
            return null;
        }

        const hasWeatherShape = ['current', 'hourly', 'daily', 'minutely', 'alerts'].some((key) => key in data);
        if (hasWeatherShape) {
            return { locationName: data.loc1 || data.location || data.name || '', payload: data };
        }

        const objectEntries = Object.entries(data).filter(([, value]) => isPlainObject(value));
        if (objectEntries.length === 1) {
            const [locationName, payload] = objectEntries[0];
            return { locationName, payload };
        }

        if (objectEntries.length > 1) {
            const [locationName, payload] = objectEntries[0];
            return { locationName, payload };
        }

        return { locationName: data.loc1 || data.name || '', payload: data };
    };

    const getCurrentWeather = (payload) => {
        if (!isPlainObject(payload)) {
            return null;
        }

        if (isPlainObject(payload.current)) {
            return payload.current;
        }

        return payload;
    };

    const buildWeatherCard = (data) => {
        const source = getWeatherSource(data);
        if (!source) {
            return '';
        }

        const payload = source.payload;
        const current = getCurrentWeather(payload);
        const weather = Array.isArray(current?.weather) ? current.weather[0] : Array.isArray(payload.weather) ? payload.weather[0] : null;
        const temperature = toCelsius(current?.temp ?? payload.temp);
        const feelsLike = toCelsius(current?.feels_like ?? payload.feels_like);
        const humidity = current?.humidity ?? payload.humidity;
        const pressure = current?.pressure ?? payload.pressure;
        const windSpeed = toKmh(current?.wind_speed ?? payload.wind_speed);
        const windDirection = current?.wind_deg ?? payload.wind_deg;
        const clouds = current?.clouds ?? payload.clouds;
        const visibility = current?.visibility ?? payload.visibility;
        const uvIndex = current?.uvi ?? payload.uvi;
        const currentTime = payload['Current UTC Time'] || payload.current_utc_time || '';
        const weekday = payload['Current Week Day UTC'] || '';
        const hourly = Array.isArray(payload.hourly) ? payload.hourly.slice(0, 4) : [];
        const daily = Array.isArray(payload.daily) ? payload.daily.slice(0, 4) : [];
        const hourlyMarkup = hourly.length
            ? `<div class="weather-chat-weather-hours">${hourly.map((entry) => {
                const entryWeather = Array.isArray(entry.weather) ? entry.weather[0] : null;
                const hourLabel = formatTime(entry.dt ? entry.dt * 1000 : null);
                const entryTemp = toCelsius(entry.temp);
                const precipitation = typeof entry.pop === 'number' ? `${Math.round(entry.pop * 100)}%` : formatPercent(entry.precipitation);

                return `
                    <div class="weather-chat-weather-hour">
                        <span class="weather-chat-weather-hour">${escapeHtml(hourLabel)}</span>
                        <strong>${escapeHtml(formatTemperature(entryTemp))}</strong>
                        <span class="weather-chat-weather-extra">${escapeHtml(precipitation)} rain</span>
                        ${entryWeather?.description ? `<span class="weather-chat-weather-extra">${escapeHtml(entryWeather.description)}</span>` : ''}
                    </div>`;
            }).join('')}</div>`
            : '';
        const dailyMarkup = daily.length
            ? `<div class="weather-chat-weather-days">${daily.map((entry) => {
                const entryWeather = Array.isArray(entry.weather) ? entry.weather[0] : null;
                const dayLabel = formatDate(entry.dt ? entry.dt * 1000 : null);
                const minTemp = toCelsius(entry.temp?.min);
                const maxTemp = toCelsius(entry.temp?.max);
                const rainChance = typeof entry.pop === 'number' ? `${Math.round(entry.pop * 100)}%` : '—';

                return `
                    <div class="weather-chat-weather-day">
                        <span class="weather-chat-weather-day">${escapeHtml(dayLabel)}</span>
                        <strong>${escapeHtml(`${formatTemperature(minTemp)} / ${formatTemperature(maxTemp)}`)}</strong>
                        <span class="weather-chat-weather-extra">${escapeHtml(rainChance)} rain</span>
                        ${entryWeather?.description ? `<span class="weather-chat-weather-extra">${escapeHtml(entryWeather.description)}</span>` : ''}
                    </div>`;
            }).join('')}</div>`
            : '';
        const stats = [
            ['Temperature', formatTemperature(temperature)],
            ['Feels like', formatTemperature(feelsLike)],
            ['Humidity', formatPercent(humidity)],
            ['Pressure', typeof pressure === 'number' ? `${Math.round(pressure)} hPa` : '—'],
            ['Wind', typeof windSpeed === 'number' ? `${formatNumber(windSpeed)} km/h` : '—']
        ];
        const extras = [
            windDirection != null ? `Wind direction ${Math.round(windDirection)}°` : '',
            clouds != null ? `Clouds ${Math.round(clouds)}%` : '',
            visibility != null ? `Visibility ${Math.round(visibility / 1000)} km` : '',
            uvIndex != null ? `UV ${formatNumber(uvIndex)}` : ''
        ].filter(Boolean);

        return `
            <div class="weather-chat-weather-card">
                <div class="weather-chat-weather-header">
                    <div>
                        <p class="weather-chat-weather-label">Weather data</p>
                        <h3 class="weather-chat-weather-title">${escapeHtml(source.locationName || payload.loc1 || 'Weather summary')}</h3>
                    </div>
                    ${currentTime || weekday ? `<span class="weather-section-tag">${escapeHtml([weekday, currentTime].filter(Boolean).join(' • '))}</span>` : ''}
                </div>

                ${weather?.description ? `<p class="weather-chat-weather-note">${escapeHtml(weather.description)}</p>` : ''}

                <div class="weather-chat-weather-grid">
                    ${stats.map(([label, value]) => `
                        <div class="weather-chat-weather-stat">
                            <p class="weather-chat-weather-stat-label">${escapeHtml(label)}</p>
                            <p class="weather-chat-weather-stat-value">${escapeHtml(value)}</p>
                        </div>`).join('')}
                </div>

                ${extras.length ? `<p class="weather-chat-weather-extra">${escapeHtml(extras.join(' · '))}</p>` : ''}
                ${hourlyMarkup}
                ${dailyMarkup}
            </div>`;
    };

    const renderMessage = (message) => {
        const article = document.createElement('article');
        article.className = `weather-chat-message weather-chat-message--${message.role}${message.typing ? ' weather-chat-message--typing' : ''}`;

        const header = document.createElement('div');
        header.className = 'weather-chat-message-meta';
        header.textContent = `${message.role === 'user' ? 'You' : 'Assistant'} · ${formatTime(message.createdAt)}`;

        const content = document.createElement('p');
        content.className = 'weather-chat-message-content';
        content.textContent = message.content;

        article.append(header, content);

        if (message.data) {
            const weatherWrapper = document.createElement('div');
            weatherWrapper.innerHTML = buildWeatherCard(message.data);
            article.append(weatherWrapper);
        }

        return article;
    };

    const renderEmptyState = () => {
        const empty = document.createElement('article');
        empty.className = 'weather-chat-message weather-chat-message--assistant';
        empty.innerHTML = `
            <p class="weather-chat-message-meta">Assistant · ready</p>
            <p class="weather-chat-message-content weather-chat-empty-state">Ask about the forecast, what to wear, or whether it is a good time to go outside.</p>
        `;
        return empty;
    };

    const renderSessions = () => {
        const orderedSessions = [...sessions].sort((left, right) => new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime());
        sessionList.innerHTML = '';

        orderedSessions.forEach((session) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `weather-chat-session-item${session.id === activeSessionId ? ' weather-chat-session-item--active' : ''}`;
            button.dataset.sessionId = session.id;

            const lastMessage = [...session.messages].reverse().find((message) => message.role === 'assistant' || message.role === 'user');
            const preview = lastMessage?.content || 'No messages yet';

            button.innerHTML = `
                <span class="weather-chat-session-title">${escapeHtml(session.title)}</span>
                <span class="weather-chat-session-preview">${escapeHtml(preview)}</span>
                <span class="weather-chat-session-time">${escapeHtml(formatDate(session.updatedAt) || 'Just now')}</span>
            `;

            sessionList.append(button);
        });
    };

    const renderMessages = () => {
        const activeSession = getActiveSession();
        sessionTitle.textContent = activeSession.title;
        messageList.innerHTML = '';

        if (!activeSession.messages.length) {
            messageList.append(renderEmptyState());
            return;
        }

        activeSession.messages.forEach((message) => {
            messageList.append(renderMessage(message));
        });

        messageList.scrollTop = messageList.scrollHeight;
    };

    const render = () => {
        persist();
        renderSessions();
        renderMessages();
    };

    const setComposerState = (disabled) => {
        chatInput.disabled = disabled;
        chatForm.querySelector('button[type="submit"]').disabled = disabled;
        createSessionButton.disabled = disabled;
    };

    const updateSessionTitle = (session, prompt) => {
        if (session.title !== 'New chat') {
            return;
        }

        const cleaned = prompt.trim().replace(/\s+/g, ' ');
        session.title = cleaned.length > 38 ? `${cleaned.slice(0, 35)}…` : cleaned;
    };

    const sendMessage = async (prompt) => {
        const activeSession = getActiveSession();
        const userMessage = {
            id: createId(),
            role: 'user',
            content: prompt,
            createdAt: new Date().toISOString()
        };

        activeSession.messages.push(userMessage);
        activeSession.updatedAt = userMessage.createdAt;
        updateSessionTitle(activeSession, prompt);
        render();
        setComposerState(true);
        isSending = true;

        const typingMessage = {
            id: createId(),
            role: 'assistant',
            content: 'Thinking about the weather…',
            createdAt: new Date().toISOString(),
            typing: true
        };
        activeSession.messages.push(typingMessage);
        pendingTypingId = typingMessage.id;
        renderMessages();

        try {
            const response = await fetch(assistantEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    prompt,
                    sessionId: activeSession.assistantSessionId
                })
            });

            const payload = await response.json().catch(() => ({}));
                if (!response.ok) {
                throw new Error(payload.error || 'Failed to load assistant response.');
            }

            activeSession.assistantSessionId = payload.session_id || activeSession.assistantSessionId;
            activeSession.messages = activeSession.messages.filter((message) => message.id !== pendingTypingId);
            activeSession.messages.push({
                id: createId(),
                role: 'assistant',
                content: payload.answer || 'I could not generate a response.',
                data: payload.data,
                createdAt: new Date().toISOString()
            });
            activeSession.updatedAt = new Date().toISOString();
            render();
        } catch (error) {
            activeSession.messages = activeSession.messages.filter((message) => message.id !== pendingTypingId);
            activeSession.messages.push({
                id: createId(),
                role: 'assistant',
                content: error.message || 'The assistant is unavailable right now.',
                createdAt: new Date().toISOString()
            });
            activeSession.updatedAt = new Date().toISOString();
            render();
        } finally {
            isSending = false;
            pendingTypingId = null;
            setComposerState(false);
        }
    };

    sessionList.addEventListener('click', (event) => {
        const button = event.target.closest('[data-session-id]');
        if (!button) {
            return;
        }

        setActiveSession(button.dataset.sessionId);
    });

    createSessionButton.addEventListener('click', () => {
        const session = createSession();
        sessions.unshift(session);
        activeSessionId = session.id;
        persist();
        render();
        chatInput.value = '';
        chatInput.focus();
    });

    chatForm.addEventListener('submit', (event) => {
        event.preventDefault();
        if (isSending) {
            return;
        }

        const prompt = chatInput.value.trim();
        if (!prompt) {
            return;
        }

        chatInput.value = '';
        sendMessage(prompt);
    });

    chatInput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            chatForm.requestSubmit();
        }
    });

    const activeSession = getActiveSession();
    upsertSession(activeSession);
    render();
})();
