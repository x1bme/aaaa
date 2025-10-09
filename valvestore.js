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
                    } else if (error.response.data.message)
