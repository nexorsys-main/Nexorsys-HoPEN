import api from "../api";

const monitoringService = {
  getStats: async () => {
    const response = await api.get("/monitoring/stats");
    return response.data;
  },
  getRecentEvents: async (limit = 50) => {
    const response = await api.get(`/monitoring/events?limit=${limit}`);
    return response.data;
  },
};

export default monitoringService;
