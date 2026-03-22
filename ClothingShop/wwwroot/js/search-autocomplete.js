// Search Autocomplete
(function() {
    let autocompleteContainer = null;
    let debounceTimer = null;
    let searchInput = null;

    // Khởi tạo autocomplete
    function initAutocomplete() {
        searchInput = document.querySelector('input[name="search"]');
        if (!searchInput) return;

        // Nếu đã khởi tạo rồi thì không làm lại
        if (searchInput.dataset.autocompleteInit) return;
        searchInput.dataset.autocompleteInit = 'true';

        // Tạo container cho autocomplete
        function createAutocompleteContainer() {
            if (autocompleteContainer) return;
            
            autocompleteContainer = document.createElement('div');
            autocompleteContainer.className = 'search-autocomplete';
            autocompleteContainer.style.cssText = `
                position: fixed;
                background: white;
                border: 1px solid #ddd;
                border-top: none;
                max-height: 400px;
                overflow-y: auto;
                z-index: 1050;
                display: none;
                box-shadow: 0 4px 6px rgba(0,0,0,0.1);
            `;
            
            // Append vào body để không bị giới hạn bởi navbar
            document.body.appendChild(autocompleteContainer);
        }
        
        // Cập nhật vị trí dropdown
        function updateDropdownPosition() {
            if (!autocompleteContainer || !searchInput) return;
            
            const rect = searchInput.getBoundingClientRect();
            autocompleteContainer.style.top = `${rect.bottom}px`;
            autocompleteContainer.style.left = `${rect.left}px`;
            autocompleteContainer.style.width = `${rect.width}px`;
        }

        // Hiển thị kết quả autocomplete
        function showAutocomplete(products) {
            if (!products || products.length === 0) {
                autocompleteContainer.style.display = 'none';
                return;
            }

            let html = '<div class="list-group list-group-flush">';
            
            products.forEach(product => {
                html += `
                    <a href="/Products/Details/${product.id}" class="list-group-item list-group-item-action">
                        <div class="d-flex align-items-center">
                            <img src="${product.imageUrl}" alt="${product.name}" 
                                 style="width: 50px; height: 50px; object-fit: cover; margin-right: 15px;">
                            <div class="flex-grow-1">
                                <div class="fw-bold text-dark">${product.name}</div>
                                <small class="text-muted">${product.category} - ${product.gender}</small>
                            </div>
                            <div class="text-danger fw-bold">${product.price.toLocaleString('vi-VN')}₫</div>
                        </div>
                    </a>
                `;
            });
            
            html += '</div>';
            
            autocompleteContainer.innerHTML = html;
            updateDropdownPosition();
            autocompleteContainer.style.display = 'block';
        }

        // Ẩn autocomplete
        function hideAutocomplete() {
            if (autocompleteContainer) {
                autocompleteContainer.style.display = 'none';
            }
        }

        // Fetch autocomplete data
        async function fetchAutocomplete(query) {
            if (query.length < 2) {
                hideAutocomplete();
                return;
            }

            try {
                const response = await fetch(`/Search/Autocomplete?q=${encodeURIComponent(query)}&limit=8`);
                const data = await response.json();
                showAutocomplete(data.suggestions);
            } catch (error) {
                console.error('Autocomplete error:', error);
                hideAutocomplete();
            }
        }

        // Event listeners
        searchInput.addEventListener('input', function(e) {
            const query = e.target.value.trim();
            
            // Debounce
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                fetchAutocomplete(query);
            }, 300);
        });

        searchInput.addEventListener('focus', function() {
            createAutocompleteContainer();
            updateDropdownPosition();
            if (this.value.trim().length >= 2) {
                fetchAutocomplete(this.value.trim());
            }
        });
        
        // Cập nhật vị trí khi scroll hoặc resize
        window.addEventListener('scroll', updateDropdownPosition);
        window.addEventListener('resize', updateDropdownPosition);

        // Ẩn khi click bên ngoài
        document.addEventListener('click', function(e) {
            if (!searchInput.contains(e.target) && !autocompleteContainer?.contains(e.target)) {
                hideAutocomplete();
            }
        });

        // Ẩn khi nhấn ESC
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' || e.key === 'Esc') {
                hideAutocomplete();
            }
        });
    }

    // Khởi tạo khi DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAutocomplete);
    } else {
        initAutocomplete();
    }
})();
