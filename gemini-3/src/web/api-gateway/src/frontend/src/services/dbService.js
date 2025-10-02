import { db } from 'boot/axios';

export default {
    getStatus() {
        return db.get('/');
    },
    getValveArchive(id) {
        return db.get(`/archives/${id}`);
    },
    // updateValve(id, valve) {
    //     return api.put(`api/Valves/${id}`, valve)
    // },
    // deleteValve(id) {
    //     return api.delete(`api/Valves/${id}`);
    // },
    // getAllConfigurations() {
    //     return api.get('/api/Valves/configurations');
    // },
    // getAllLogs() {
    //     return api.get('/api/Valves/logs');
    // },
    // getValveLogs(id) {
    //     return api.get(`/api/Valves/${id}/logs`)
    // }

}