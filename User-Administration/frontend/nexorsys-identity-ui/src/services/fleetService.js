import api from "../api";

const fleetService = {
  getFleetWorkstations: async () => {
    const response = await api.get("/fleet/workstations");
    return response.data;
  },
};

export default fleetService;
