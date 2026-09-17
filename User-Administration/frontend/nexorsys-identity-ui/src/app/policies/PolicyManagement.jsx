import React, { useState, useEffect } from "react";
import {
  Table,
  Card,
  Button,
  Modal,
  Form,
  Input,
  Select,
  TimePicker,
  message,
  Space,
  Popconfirm,
  Alert,
} from "antd";
import { PlusOutlined, EditOutlined, DeleteOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import policyService from "../../services/policyService";
import { requestFailureMessage } from "../../security/authState";

const { Option } = Select;

export default function PolicyManagement() {
  const [policies, setPolicies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [editingPolicy, setEditingPolicy] = useState(null);
  const [form] = Form.useForm();
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    loadPolicies();
  }, []);

  const loadPolicies = async () => {
    try {
      setLoading(true);
      setLoadError(null);
      const data = await policyService.getPolicies();
      setPolicies(data);
    } catch (error) {
      setLoadError(requestFailureMessage(error, "service de politiques"));
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (values) => {
    try {
      const formattedValues = {
        ...values,
        allowedAccessStart: values.allowedAccessStart.format("HH:mm:ss"),
        allowedAccessEnd: values.allowedAccessEnd.format("HH:mm:ss"),
      };

      if (editingPolicy) {
        await policyService.updatePolicy(editingPolicy.id, formattedValues);
        message.success("Politique mise à jour");
      } else {
        await policyService.createPolicy(formattedValues);
        message.success("Politique créée");
      }
      setIsModalVisible(false);
      loadPolicies();
    } catch (error) {
      message.error("Échec de l’enregistrement de la politique");
    }
  };

  const handleDelete = async (id) => {
    try {
      await policyService.deletePolicy(id);
      message.success("Politique supprimée");
      loadPolicies();
    } catch (error) {
      message.error("Échec de la suppression de la politique");
    }
  };

  const openModal = (policy = null) => {
    setEditingPolicy(policy);
    if (policy) {
      form.setFieldsValue({
        department: policy.department,
        requiredRiskLevel: policy.requiredRiskLevel,
        allowedAccessStart: dayjs(policy.allowedAccessStart, "HH:mm:ss"),
        allowedAccessEnd: dayjs(policy.allowedAccessEnd, "HH:mm:ss"),
      });
    } else {
      form.resetFields();
    }
    setIsModalVisible(true);
  };

  return (
    <div style={{ padding: 24 }}>
      <Card
        title="Politiques d’authentification par département"
        extra={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => openModal()}
          >
            Ajouter une politique
          </Button>
        }
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Configuration enregistrée, mais non appliquée à l’authentification"
          description="Les niveaux de risque et horaires de cette page ne sont pas encore reliés au flux d’authentification. Ne les utilisez pas comme contrôle d’accès effectif."
        />
        {loadError && <Alert type="error" showIcon message={loadError} style={{ marginBottom: 16 }} />}
        <Table
          dataSource={policies}
          rowKey="id"
          loading={loading}
          locale={{ emptyText: "Aucune donnée" }}
          columns={[
            { title: "Département", dataIndex: "department", key: "department" },
            {
              title: "Niveau de risque requis",
              dataIndex: "requiredRiskLevel",
              key: "requiredRiskLevel",
            },
            {
              title: "Début d’accès",
              dataIndex: "allowedAccessStart",
              key: "allowedAccessStart",
            },
            {
              title: "Fin d’accès",
              dataIndex: "allowedAccessEnd",
              key: "allowedAccessEnd",
            },
            {
              title: "Actions",
              key: "actions",
              render: (_, record) => (
                <Space>
                  <Button
                    icon={<EditOutlined />}
                    onClick={() => openModal(record)}
                  />
                  <Popconfirm
                    title="Supprimer cette politique ?"
                    onConfirm={() => handleDelete(record.id)}
                  >
                    <Button danger icon={<DeleteOutlined />} />
                  </Popconfirm>
                </Space>
              ),
            },
          ]}
        />
      </Card>

      <Modal
        title={editingPolicy ? "Modifier la politique" : "Créer une politique"}
        open={isModalVisible}
        onOk={() => form.submit()}
        onCancel={() => setIsModalVisible(false)}
      >
        <Form form={form} layout="vertical" onFinish={handleSave}>
          <Form.Item
            name="department"
            label="Département"
            rules={[{ required: true }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="requiredRiskLevel"
            label="Niveau de risque requis"
            rules={[{ required: true }]}
          >
            <Select>
              <Option value="Low">Faible</Option>
              <Option value="Medium">Moyen</Option>
              <Option value="High">Élevé</Option>
            </Select>
          </Form.Item>
          <Form.Item
            name="allowedAccessStart"
            label="Heure de début d’accès"
            rules={[{ required: true }]}
          >
            <TimePicker format="HH:mm:ss" style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item
            name="allowedAccessEnd"
            label="Heure de fin d’accès"
            rules={[{ required: true }]}
          >
            <TimePicker format="HH:mm:ss" style={{ width: "100%" }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
