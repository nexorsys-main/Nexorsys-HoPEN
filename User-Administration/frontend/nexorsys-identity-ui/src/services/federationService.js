import api from "../api";

const federationService = {
  getActiveProviders: async () => {
    try {
      const response = await api.get("/federation/providers");
      return response.data?.providers || [];
    } catch (error) {
      if (error.response?.status === 501) {
        return [];
      }
      throw error;
    }
  },
};

export default federationService;
