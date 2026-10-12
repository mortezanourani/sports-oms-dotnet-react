import { Modal, Table, Form, Select, DatePicker, Button, message, Tag, Divider } from 'antd';
import { useFederations } from '../../../hooks/useFederations';
import { useAssignFederationPosition, useUserFederationPositions } from '../../../hooks/useUsers';
import { FEDERATION_POSITION_LABELS } from '../../../constants/labels';

export default function AssignPositionModal({ user, open, onClose }) {
  const [form] = Form.useForm();
  const { data: federations } = useFederations();
  const { data: history, isLoading } = useUserFederationPositions(user?.id);
  const assignMutation = useAssignFederationPosition();

  const handleAssign = async (values) => {
    try {
      await assignMutation.mutateAsync({
        userId: user.id,
        payload: {
          ...values,
          startDate: values.startDate.format('YYYY-MM-DD'),
          endDate: values.endDate ? values.endDate.format('YYYY-MM-DD') : null,
        },
      });
      message.success('سمت با موفقیت تخصیص داده شد');
      form.resetFields();
    } catch (err) {
      message.error(err.response?.data?.detail || 'خطایی رخ داد');
    }
  };

  const columns = [
    { title: 'فدراسیون', dataIndex: 'federationName' },
    { title: 'سمت', dataIndex: 'position', render: (v) => FEDERATION_POSITION_LABELS[v] || v },
    { title: 'از تاریخ', dataIndex: 'startDate' },
    {
      title: 'تا تاریخ', dataIndex: 'endDate',
      render: (v) => (v ? v : <Tag color="green">در حال حاضر</Tag>),
    },
  ];

  return (
    <Modal
      title={user ? `سوابق و تخصیص سمت \u2014 ${user.fullName}` : 'تخصیص سمت'}
      open={open}
      onCancel={onClose}
      footer={null}
      width={700}
      destroyOnClose
    >
      <Table rowKey="id" columns={columns} dataSource={history} loading={isLoading} pagination={false}
        locale={{ emptyText: 'سابقه‌ای ثبت نشده است' }} />

      <Divider />

      <Form form={form} layout="vertical" onFinish={handleAssign}>
        <Form.Item name="federationId" label="فدراسیون" rules={[{ required: true }]}>
          <Select options={federations?.map((f) => ({ value: f.id, label: f.name }))} />
        </Form.Item>
        <Form.Item name="position" label="سمت" rules={[{ required: true }]}>
          <Select options={Object.entries(FEDERATION_POSITION_LABELS).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
        <Form.Item name="startDate" label="تاریخ شروع" rules={[{ required: true }]}>
          <DatePicker style={{ width: '100%' }} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={assignMutation.isPending}>تخصیص سمت</Button>
      </Form>
    </Modal>
  );
}
