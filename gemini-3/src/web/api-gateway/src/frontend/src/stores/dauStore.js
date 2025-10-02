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
        availableDaus: (state) => state.daus.filter(d => !d.valveId || d.valveId === 0),
        attachedDaus: (state) => state.daus.filter(d => d.valveId),
        isLoading: (state) => state.loading
    },
    actions: {
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
        async fetchDauById(id) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.get(`/api/dau/${id}`);
                return response.data;
            } catch (error) {
                this.error = error.message || `Failed to fetch DAU with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async createDau(dau) {
            this.loading = true;
            this.error = null;
            try {
                const response = await api.post('/api/dau', dau);
                this.daus.push(response.data);
                return response.data;
            } catch (error) {
                this.error = error.message || 'Failed to create DAU';
                throw error;
            } finally {
                this.loading = false;
            }
        },
        async updateDau(id, dau) {
            this.loading = true;
            this.error = null;
            try {
                await api.put(`/api/dau?dauId=${id}`, dau);
                await this.fetchDaus(); // Refresh the daus list
                return true;
            } catch (error) {
                this.error = error.message || `Failed to update DAU with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
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
                this.error = error.message || `Failed to delete DAU with ID ${id}`;
                throw error;
            } finally {
                this.loading = false;
            }
        },
        clearCurrentDau() {
            this.currentDau = null;
        }
    }
});