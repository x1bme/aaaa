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
        activeValves: (state) => state.valves.filter(v => {
            // A valve is considered active if:
            // 1. It has isActive flag set to true
            // 2. Both DAUs are attached
            // 3. Neither DAU is offline
            return v.isActive && 
                   v.atv && v.remote &&
                   v.atv.status !== 3 && v.remote.status !== 3;
        }),
        inactiveValves: (state) => state.valves.filter(v => !v.isActive),
        offlineValves: (state) => state.valves.filter(v => 
            !v.atv || !v.remote || v.atv.status === 3 || v.remote.status === 3
        ),
        valvesNeedingAttention: (state) => state.valves.filter(v => 
            v.atv && v.remote && 
            (v.atv.status === 1 || v.atv.status === 2 || v.remote.status === 1 || v.remote.status === 2)
        ),
        inoperableValves: (state) => state.valves.filter(v => !v.atv || !v.remote),
        isLoading: (state) => state.loading
    },
    actions: {
        async fetchValves() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves');
                this.valves = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch valves';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async fetchValveById(id) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get(`/api/valves/${id}`);
                this.currentValve = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || `Failed to fetch valve with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async createValve(valve) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.post('/api/valves', valve);
                this.valves.push(response.data);
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to create valve';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async updateValve({id, valve}) {
            this.loading = true;
            this.error = null;
            try {
                await api.put(`/api/valves/${id}`, valve);
                
                // Refresh the valve data
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
                this.error = error.message || `Failed to update valve with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
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
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async fetchConfigs() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves/configurations');
                this.configurations = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch configurations';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async fetchLogs() {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get('/api/valves/logs');
                this.logs = response.data;
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to fetch logs';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        clearCurrentValve() {
            this.currentValve = null;
        }
    }
});