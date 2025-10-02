import { api } from 'boot/axios';

export default {
    getAllValves() {
        return api.get('/api/Valves');
    },
    getValveById(id) {
        return api.get(`/api/Valves/${id}`);
    },
    createValve(valve) {
        return api.post(`/api/Valves`, valve);
    },
    updateValve(id, valve) {
        return api.put(`api/Valves/${id}`, valve)
    },
    deleteValve(id) {
        return api.delete(`api/Valves/${id}`);
    },
    getAllConfigurations() {
        return api.get('/api/Valves/configurations');
    },
    getAllLogs() {
        return api.get('/api/Valves/logs');
    },
    getValveLogs(id) {
        return api.get(`/api/Valves/${id}/logs`)
    }

}