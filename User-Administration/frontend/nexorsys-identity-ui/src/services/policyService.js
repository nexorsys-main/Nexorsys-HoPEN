import api from "../api";

const policyService = {
  getPolicies: async () => {
    const response = await api.get("/policies");
    return response.data;
  },
  createPolicy: async (policy) => {
    const response = await api.post("/policies", policy);
    return response.data;
  },
  updatePolicy: async (id, policy) => {
    const response = await api.put(`/policies/${id}`, policy);
    return response.data;
  },
  deletePolicy: async (id) => {
    const response = await api.delete(`/policies/${id}`);
    return response.data;
  },
};

export default policyService;
