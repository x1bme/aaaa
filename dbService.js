import { api } from 'boot/axios';

export default {
    // Get archiver service status
    getStatus() {
        return api.get('/api/database/status');
    },
    
    // Get valve archive data (download latest test for valve)
    getValveArchive(valveId) {
        return api.get(`/api/database/archives/${valveId}`, {
            responseType: 'blob'
        });
    },
    
    // Get all tests for a valve
    getTestsByValve(valveId) {
        return api.get(`/api/database/tests`, {
            params: { valveId }
        });
    },
    
    // Get test metadata by ID
    getTest(testId) {
        return api.get(`/api/database/tests/${testId}`);
    },
    
    // Download test file
    downloadTest(testId) {
        return api.get(`/api/database/tests/${testId}/download`, {
            responseType: 'blob'
        });
    },
    
    // Upload a new test
    uploadTest(valveId, file) {
        const formData = new FormData();
        formData.append('file', file);
        
        return api.post(`/api/database/tests?valveId=${valveId}`, formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        });
    },
    
    // Download database backup
    getDatabaseBackup() {
        return api.get('/api/database/backup', {
            responseType: 'blob'
        });
    }
};
