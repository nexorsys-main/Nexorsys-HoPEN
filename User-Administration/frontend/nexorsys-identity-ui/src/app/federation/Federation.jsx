import React, { useState, useEffect } from "react";
import { Card, Table, Tag, Alert } from "antd";
import federationService from "../../services/federationService";
import { requestFailureMessage } from "../../security/authState";

export default function Federation() {
  const [providers, setProviders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    loadProviders();
  }, []);

  const loadProviders = async () => {
    try {
      setLoading(true);
      setLoadError(null);
      const data = await federationService.getActiveProviders();
      setProviders(data);
    } catch (error) {
      setLoadError(requestFailureMessage(error, "service de fédération"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ padding: 24 }}>
      <Card title="External Identity Providers (Federation)">
        <p>
          Manage external authentication providers (PSI, e-CPS, OAuth2) linked
          to the NexorSys Identity platform.
        </p>
        {loadError && <Alert type="error" showIcon message={loadError} style={{ marginBottom: 16 }} />}
        <Table
          dataSource={providers}
          rowKey="providerId"
          loading={loading}
          columns={[
            { title: "Provider", dataIndex: "name", key: "name" },
            {
              title: "Status",
              dataIndex: "status",
              key: "status",
              render: (status) => (
                <Tag color={status === "Online" ? "green" : "default"}>
                  {status}
                </Tag>
              ),
            },
            { title: "Priority", dataIndex: "priority", key: "priority" },
          ]}
        />
      </Card>
    </div>
  );
}
