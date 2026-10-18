import { useState } from 'react';
import { Button, Form, Input, message, Modal, Table } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useCreateFacilityContract, useFacilityContracts } from '../../../hooks/useFacilities';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { formatSolarDate, toUtcDateTime } from '../../../utils/dates';

export default function ContractHistoryModal({ facility, open, onClose }) {
    const [addModalOpen, setAddModalOpen] = useState(false);
    const [form] = Form.useForm();
    const { data: contracts, isLoading } = useFacilityContracts(facility?.id, open);
    const createMutation = useCreateFacilityContract();

    const closeAddModal = () => {
        if (createMutation.isPending) return;
        setAddModalOpen(false);
        form.resetFields();
    };

    const closeHistoryModal = () => {
        if (createMutation.isPending) return;
        setAddModalOpen(false);
        form.resetFields();
        onClose();
    };

    const handleCreate = async (values) => {
        try {
            const payload = {
                ...values,
                startDate: toUtcDateTime(values.startDate),
                endDate: toUtcDateTime(values.endDate),
            };
            await createMutation.mutateAsync({ facilityId: facility.id, payload });
            message.success('قرارداد با موفقیت ثبت شد');
            setAddModalOpen(false);
            form.resetFields();
        } catch (error) {
            message.error(error.response?.data?.detail || 'خطا در ثبت قرارداد');
        }
    };

    const columns = [
        { title: 'شماره قرارداد', dataIndex: 'contractNumber' },
        { title: 'طرف قرارداد', dataIndex: 'thirdPartyName' },
        { title: 'تماس', dataIndex: 'thirdPartyContact' },
        { title: 'از تاریخ', dataIndex: 'startDate', render: formatSolarDate },
        { title: 'تا تاریخ', dataIndex: 'endDate', render: formatSolarDate },
        { title: 'فایل قرارداد', dataIndex: 'contractFilePath', render: (value) => value || '—' },
    ];

    return (
        <>
            <Modal
                title={facility ? `سوابق قراردادهای ${facility.name}` : 'سوابق قراردادها'}
                open={open}
                onCancel={closeHistoryModal}
                closable={!createMutation.isPending}
                maskClosable={!createMutation.isPending}
                footer={null}
                width={900}
                destroyOnClose
            >
                <div style={{ marginBottom: 16 }}>
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => setAddModalOpen(true)}>
                        افزودن قرارداد جدید
                    </Button>
                </div>
                <Table
                    rowKey="id"
                    columns={columns}
                    dataSource={contracts}
                    loading={isLoading}
                    pagination={false}
                    scroll={{ x: true }}
                    locale={{ emptyText: 'قراردادی ثبت نشده است' }}
                />
            </Modal>
            <Modal
                title="افزودن قرارداد جدید"
                open={addModalOpen}
                onCancel={closeAddModal}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending}
                okText="ثبت"
                cancelText="انصراف"
                closable={!createMutation.isPending}
                maskClosable={!createMutation.isPending}
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleCreate}>
                    <Form.Item name="contractNumber" label="شماره قرارداد">
                        <Input />
                    </Form.Item>
                    <Form.Item name="thirdPartyName" label="طرف قرارداد">
                        <Input />
                    </Form.Item>
                    <Form.Item name="thirdPartyContact" label="شماره تماس">
                        <Input />
                    </Form.Item>
                    <Form.Item
                        name="startDate"
                        label="تاریخ شروع"
                        rules={[{ required: true, message: 'تاریخ شروع قرارداد الزامی است' }]}
                    >
                        <SolarDatePicker />
                    </Form.Item>
                    <Form.Item name="endDate" label="تاریخ پایان">
                        <SolarDatePicker />
                    </Form.Item>
                    <Form.Item name="contractFilePath" label="مسیر فایل قرارداد">
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </>
    );
}
