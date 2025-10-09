import { defineStore } from "pinia";
import { api } from "src/boot/axios";

export const useValveStore = defineStore('valve', {
    state: () => ({
        valves: [],
        currentValve: null,
        configurations: [],
        logs: [],
        loading: false,
        error: null
    }),
    getters: {
        allValves: (state) => state.valves,
        
        // Active valves: has isActive flag AND both DAUs attached and operational
        activeValves: (state) => state.valves.filter(v => {
            return v.isActive && 
                   v.atv && v.remote &&
                   v.atv.status !== 3 && v.remote.status !== 3;
        }),
        
        // Inactive valves: isActive flag is false
        inactiveValves: (state) => state.valves.filter(v => !v.isActive),
        
        // Offline valves: Missing DAUs or DAUs are offline
        offlineValves: (state) => state.valves.filter(v => 
            !v.atv || !v.remote || v.atv.status === 3 || v.remote.status === 3
        ),
        
        // Valves needing attention: Has both DAUs but one/both need calibration or maintenance
        valvesNeedingAttention: (state) => state.valves.filter(v => 
            v.atv && v.remote && 
            (v.atv.status === 1 || v.atv.status === 2 || 
             v.remote.status === 1 || v.remote.status === 2)
        ),
        
        // Inoperable valves: Missing one or both DAUs
        inoperableValves: (state) => state.valves.filter(v => !v.atv || !v.remote),
        
        isLoading: (state) => state.loading
    },
    actions: {
        // Fetch all valves (with enriched DAU data from gRPC)
        async fetchValves() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves');
                this.valves = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch valves';
                console.error('Error fetching valves:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch single valve by ID
        async fetchValveById(id) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get(`/api/valves/${id}`);
                this.currentValve = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || `Failed to fetch valve with ID ${id}`;
                console.error('Error fetching valve:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Create a new valve
        async createValve(valve) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.post('/api/valves', valve);
                this.valves.push(response.data);
                return response.data;
            } catch (error) {
                this.error = error.response?.data || error.message || 'Failed to create valve';
                console.error('Error creating valve:', error);
                
                // Extract user-friendly error message
                if (error.response?.data) {
                    if (typeof error.response.data === 'string') {
                        throw new Error(error.response.data);
                    } else if (error.response.data.message) {
                        throw new Error(error.response.data.message);
                    } else if (error.response.data.title) {
                        throw new Error(error.response.data.title);
                    }
                }
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Update an existing valve
        async updateValve({id, valve}) {
            this.loading = true;
            this.error = null;
            try {
                await api.put(`/api/valves/${id}`, valve);
                
                // Refresh the valve data to get updated DAU info
                const response = await api.get(`/api/valves/${id}`);
                const updatedValve = response.data;
                
                // Update the valve in the valves array
                const index = this.valves.findIndex(v => v.id === id);
                if (index !== -1) {
                    this.valves.splice(index, 1, updatedValve);
                } else {
                    this.valves.push(updatedValve);
                }
                
                if (this.currentValve?.id === id) {
                    this.currentValve = updatedValve;
                }
                
                return updatedValve;
            } catch (error) {
                this.error = error.response?.data || error.message || `Failed to update valve with ID ${id}`;
                console.error('Error updating valve:', error);
                
                // Extract user-friendly error message
                if (error.response?.data) {
                    if (typeof error.response.data === 'string') {
                        throw new Error(error.response.data);
                    } else if (error.response.data.message) {
                        throw new Error(error.response.data.message);
                    } else if (error.response.data.title) {
                        throw new Error(error.response.data.title);
                    }
                }
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Delete a valve (this will also detach the DAUs)
        async deleteValve(id) {
            this.loading = true;
            this.error = null;
            try {
                await api.delete(`/api/valves/${id}`);
                this.valves = this.valves.filter(v => v.id !== id);
                if (this.currentValve?.id === id) {
                    this.currentValve = null;
                }
                return true;
            } catch (error) {
                this.error = error.message || `Failed to delete valve with ID ${id}`;
                console.error('Error deleting valve:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch all valve configurations
        async fetchConfigs() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves/configurations');
                this.configurations = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch configurations';
                console.error('Error fetching configurations:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Fetch all valve logs
        async fetchLogs() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves/logs');
                this.logs = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch logs';
                console.error('Error fetching logs:', error);
                throw error;
            } finally {
                this.loading = false;
            }
        },
        
        // Clear current valve
        clearCurrentValve() {
            this.currentValve = null;
        },
        
        // Clear error
        clearError() {
            this.error = null;
        }
    }
});
