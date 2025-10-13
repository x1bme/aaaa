import { defineStore } from "pinia";
import { api } from "src/boot/axios";

export const useDauStore = defineStore('dau', {
    state: () => ({
        daus: [],
        currentDau: null,
        loading: false,
        error: null
    }),
    getters: {
        allDaus: (state) => state.daus,
        
        // Get only registered DAUs
        registeredDaus: (state) => state.daus.filter(d => d.registered),
        
        // Get DAUs available for valve assignment (registered but not attached to a valve)
        availableDaus: (state) => state.daus.filter(d => 
            d.registered && (!d.valveId || d.valveId === 0)
        ),
        
        // Get DAUs attached to valves
        attachedDaus: (state) => state.daus.filter(d => d.valveId),
        
        isLoading: (state) => state.loading
    },
    actions: {
        // Fetch all registered DAUs (with live data from gRPC)
        async fetchDaus() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/dau');
                this.daus = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch DAUs';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch unregistered DAUs from server
        async fetchUnregisteredDaus() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/dau/unregistered');
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch unregistered DAUs';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch all DAUs from server (via gRPC)
        async fetchServerDaus() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/dau/server');
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch DAUs from server';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch single DAU by ID (with live data from gRPC)
        async fetchDauById(id) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get(`/api/dau/${id}`);
                this.currentDau = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || `Failed to fetch DAU with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Register a new DAU
        async createDau(dau) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.post('/api/dau', dau);
                this.daus.push(response.data);
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to register DAU';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Update a DAU
        async updateDau(id, dau) {
            this.loading = true;
            this.error = null;
            try {
                // FIXED: The backend expects dauId as query parameter AND the dau object in body
                // The backend signature is: UpdateDau(int dauId, Dau dau)
                // So we need to include the id in the dau object for model binding
                const dauWithId = {
                    id: id, // Backend model binding needs this
                    ...dau
                };
                
                await api.put(`/api/dau?dauId=${id}`, dauWithId);
                
                // Refresh the DAU to get updated data from server (with gRPC enrichment)
                const updatedDau = await this.fetchDauById(id);
                
                // Update in local state
                const index = this.daus.findIndex(d => d.id === id);
                if (index !== -1) {
                    this.daus.splice(index, 1, updatedDau);
                } else {
                    this.daus.push(updatedDau);
                }
                
                if (this.currentDau?.id === id) {
                    this.currentDau = updatedDau;
                }
                
                return updatedDau;
            } catch (error) {
                this.error = error.message || `Failed to update DAU with ID ${id}`;
                console.error('Error updating DAU:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Unregister a DAU
        async deleteDau(id) {
            this.loading = true;
            this.error = null;
            try {
                await api.delete(`/api/dau/${id}`);
                this.daus = this.daus.filter(d => d.id !== id);
                if (this.currentDau?.id === id) {
                    this.currentDau = null;
                }
                return true;
            } catch (error) {
                this.error = error.message || `Failed to unregister DAU with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        clearCurrentDau() {
            this.currentDau = null;
        },
        
        clearError() {
            this.error = null;
        }
    }
});
