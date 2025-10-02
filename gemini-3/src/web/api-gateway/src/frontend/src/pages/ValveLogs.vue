<template>
  <q-page class="log-page">
    <div class="header">
            <h4><u>Loom System Logs</u></h4>
      <q-btn
        color="primary"
        label="Export Logs"
        icon="download"
        @click="exportLogs"
      />
    </div>
    <div>
      <div v-for="(item, index) in logs" :key="index" class="log-item">
        [{{ formatTime(item.time) }}] {{ item.message }}
      </div>
    </div>
  </q-page>
</template>

<script>
export default {
  data() {
    return {
      logs: [
        { time: '2024-03-13T08:30:00Z', message: 'DAU 153A powered on and initialized.' },
        { time: '2024-03-13T08:32:00Z', message: 'DAU 153A starting data  collection process.' },
        { time: '2024-03-13T08:34:00Z', message: 'Data collection started on DAU 153A.' },
        { time: '2024-03-13T08:36:00Z', message: 'Sending data from DAU 153A to Gemini Server PID 158.' },
        { time: '2024-03-13T08:38:00Z', message: 'Data transmission successful from DAU 153A.' },
        { time: '2024-03-13T08:40:00Z', message: 'DAU 153B powered on and initialized.' },
        { time: '2024-03-13T08:42:00Z', message: 'DAU 153B starting data collection process.' },
        { time: '2024-03-13T08:44:00Z', message: 'Data collection started on DAU 153B.' },
        { time: '2024-03-13T08:46:00Z', message: 'Sending data from DAU 153B to Gemini Server PID 158.' },
        { time: '2024-03-13T08:48:00Z', message: 'Data transmission successful from DAU 153B.' },
        { time: '2024-03-13T08:50:00Z', message: 'DAU 829A powered on and initialized.' },
        { time: '2024-03-13T08:52:00Z', message: 'DAU 829A starting data collection process.' },
        { time: '2024-03-13T08:54:00Z', message: 'Data collection started on DAU 829A.' },
        { time: '2024-03-13T08:56:00Z', message: 'Sending data from DAU 829A to Gemini Server PID 158.' },
        { time: '2024-03-13T08:58:00Z', message: 'Data transmission successful from DAU 829A.' },
        { time: '2024-03-13T09:00:00Z', message: 'DAU 829B powered on and initialized.' },
        { time: '2024-03-13T09:02:00Z', message: 'DAU 829B starting data collection process.' },
        { time: '2024-03-13T09:04:00Z', message: 'Data collection started on DAU 829B.' },
        { time: '2024-03-13T09:06:00Z', message: 'Sending data from DAU 829B to Gemini Server PID 158.' },
        { time: '2024-03-13T09:08:00Z', message: 'Data transmission successful from DAU 829B.' },
        { time: '2024-03-13T09:10:00Z', message: 'Communication error between DAUs 153A and DAU 153B.' },
        { time: '2024-03-13T09:12:00Z', message: 'Attempting to re-establish communication between DAUs 153A and DAU 153B.' },
        { time: '2024-03-13T09:14:00Z', message: 'Communication restored between DAUs 153A and DAU 153B.' },
        { time: '2024-03-13T09:16:00Z', message: 'Communication error between DAUs 829A and 829B.' },
        { time: '2024-03-13T09:18:00Z', message: 'Attempting to re-establish communication between DAUs 829A and 829B.' },
        { time: '2024-03-13T09:20:00Z', message: 'Communication restored between DAUs 829A and 829B.' },
        { time: '2024-03-13T09:22:00Z', message: 'DAU 156A powered on and initialized.' },
        { time: '2024-03-13T09:24:00Z', message: 'DAU 156A starting data collection process.' },
        { time: '2024-03-13T09:26:00Z', message: 'Data collection started on DAU 156A.' },
        { time: '2024-03-13T09:28:00Z', message: 'Sending data from DAU 156A to Gemini Server PID 158.' },
        { time: '2024-03-13T09:30:00Z', message: 'Data transmission successful from DAU 156A.' },
        { time: '2024-03-13T09:32:00Z', message: 'DAU 156B powered on and initialized.' },
        { time: '2024-03-13T09:34:00Z', message: 'DAU 156B starting data collection process.' },
        { time: '2024-03-13T09:36:00Z', message: 'Data collection started on DAU 156B.' },
        { time: '2024-03-13T09:38:00Z', message: 'Sending data from DAU 156B to Gemini Server PID 158.' },
        { time: '2024-03-13T09:40:00Z', message: 'Data transmission successful from DAU 156B.' },
        { time: '2024-03-13T09:42:00Z', message: 'DAU 623A powered on and initialized.' },
        { time: '2024-03-13T09:44:00Z', message: 'DAU 623A starting data collection process.' },
        { time: '2024-03-13T09:46:00Z', message: 'Data collection started on DAU 623A.' },
        { time: '2024-03-13T09:48:00Z', message: 'Sending data from DAU 623A to Gemini Server PID 158.' },
        { time: '2024-03-13T09:50:00Z', message: 'Data transmission successful from DAU 623A.' },
        { time: '2024-03-13T09:52:00Z', message: 'DAU 623B powered on and initialized.' },
        { time: '2024-03-13T09:54:00Z', message: 'DAU 623B starting data collection process.' },
        { time: '2024-03-13T09:56:00Z', message: 'Data collection started on DAU 623B.' },
        { time: '2024-03-13T09:58:00Z', message: 'Sending data from DAU 623B to Gemini Server PID 158.' },
        { time: '2024-03-13T10:00:00Z', message: 'Data transmission successful from DAU 623B.' },
        { time: '2024-03-13T10:02:00Z', message: 'Error: DAU 153A failed to send data to Gemini Server PID 158.' },
        { time: '2024-03-13T10:04:00Z', message: 'Attempting to reset DAU153A.' },
        { time: '2024-03-13T10:06:00Z', message: 'Reset successful on DAU153A.' },
        { time: '2024-03-13T10:08:00Z', message: 'Re-attempting data transmission from DAU 153A to Gemini Server PID 158.' },
        { time: '2024-03-13T10:10:00Z', message: 'Data transmission successful from DAU 153A.' },
        { time: '2024-03-13T10:12:00Z', message: 'Error: DAU 829B failed to send data to Gemini Server PID 158.' },
        { time: '2024-03-13T10:14:00Z', message: 'Attempting to reset DAU829B.' },
        { time: '2024-03-13T10:16:00Z', message: 'Reset successful on DAU829B.' },
        { time: '2024-03-13T10:18:00Z', message: 'Re-attempting data transmission from DAU 829B to Gemini Server PID 158.' },
        { time: '2024-03-13T10:20:00Z', message: 'Data transmission successful from DAU 829B.' },
        { time: '2024-03-13T10:22:00Z', message: 'All devices reporting data successfully to Gemini Server PID 158.' },
        { time: '2024-03-13T10:24:00Z', message: 'Data synchronization confirmed across all hardware devices.' },
        { time: '2024-03-13T10:26:00Z', message: 'DAU 156A detected anomaly during data collection.' },
        { time: '2024-03-13T10:28:00Z', message: 'Anomaly investigation initiated on DAU 156A.' },
        { time: '2024-03-13T10:30:00Z', message: 'Investigation of anomaly on DAU 156A completed.' },
        { time: '2024-03-13T10:32:00Z', message: 'Anomaly resolved on DAU 156A.' },
        { time: '2024-03-13T10:34:00Z', message: 'Data collection resumed on DAU 156A.' },
        { time: '2024-03-13T10:36:00Z', message: 'DAU 156B detected anomaly during data collection.' },
        { time: '2024-03-13T10:38:00Z', message: 'Anomaly investigation initiated on DAU 156B.' },
        { time: '2024-03-13T10:40:00Z', message: 'Investigation of anomaly on DAU 156B completed.' },
        { time: '2024-03-13T10:42:00Z', message: 'Anomaly resolved on DAU 156B.' }, // Correction: Should be 3-2
        { time: '2024-03-13T10:44:00Z', message: 'Data collection resumed on DAU 156B.' },
        { time: '2024-03-13T10:48:00Z', message: 'Heartbeat, normal operation on all Valve Networks'},
        { time: '2024-03-13T12:48:00Z', message: 'Heartbeat, normal operation on all Valve Networks'},
        { time: '2024-03-13T14:48:00Z', message: 'Heartbeat, normal operation on all Valve Networks'},
        { time: '2024-03-13T16:48:00Z', message: 'Heartbeat, normal operation on all Valve Networks'},
        { time: '2024-03-13T18:48:00Z', message: 'Heartbeat, normal operation on all Valve Networks'},
        { time: '2024-03-13T20:48:00Z', message: 'Heartbeat, normal operaton on all Valve Networks'},
        { time: '2024-03-13T22:48:00Z', message: 'Heartbeat, normal operaton on all Valve Networks'},
        { time: '2024-03-14T04:01:00Z', message: 'pi day, time to go home, all devices shutting down' },
      ],
    };
  },
  methods: {
    formatTime(timeString) {
      const date = new Date(timeString);
      return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    },
    exportLogs() {
      // Create text content
      const textContent = this.logs.map(log => 
        `[${this.formatTime(log.time)}] ${log.message}`
      ).join('\n');
      
      const blob = new Blob([textContent], { type: 'text/plain;charset=utf-8;' });
      const link = document.createElement('a');
      if (link.download !== undefined) {
        const url = URL.createObjectURL(blob);
        link.setAttribute('href', url);
        link.setAttribute('download', 'system_logs.txt');
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      }
    }
  }
}
</script>

<style scoped>
.log-page {
  background-color: white;
  padding: 20px;
  font-family: monospace;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.log-item {
  margin-bottom: -4px;
  color: black;
}
</style>
