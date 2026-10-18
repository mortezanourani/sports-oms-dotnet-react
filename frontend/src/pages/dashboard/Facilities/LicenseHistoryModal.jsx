import { useState } from 'react';
import { Button, Form, Input, message, Modal, Table } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useCreateFacilityLicense, useFacilityLicenses } from '../../../hooks/useFacilities';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { formatSolarDate, toUtcDateTime } from '../../../utils/dates';

export default function LicenseHistoryModal({ facility, open, onClose }) {
    const [addModalOpen, setAddModalOpen] = useState(false);
    const [form] = Form.useForm();
    const { data: licenses, isLoading } = useFacilityLicenses(facility?.id, open);
    const createMutation = useCreateFacilityLicense();

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
                issueDate: toUtcDateTime(values.issueDate),
                expiryDate: toUtcDateTime(values.expiryDate),
            };
            await createMutation.mutateAsync({ facilityId: facility.id, payload });
            message.success('مجوز با موفقیت ثبت شد');
            setAddModalOpen(false);
            form.resetFields();
        } catch (error) {
            message.error(error.response?.data?.detail || 'خطا در ثبت مجوز');
        }
    };

    const columns = [
        { title: 'شماره مجوز', dataIndex: 'licenseNumber' },
        { title: 'دارنده مجوز', dataIndex: 'holderName' },
        { title: 'تاریخ صدور', dataIndex: 'issueDate', render: formatSolarDate },
        { title: 'تاریخ انقضا', dataIndex: 'expiryDate', render: formatSolarDate },
        { title: 'فایل مجوز', dataIndex: 'licenseFilePath', render: (value) => value || '—' },
    ];

    return (
        <>
            <Modal
                title={facility ? `سوابق مجوزهای ${facility.name}` : 'سوابق مجوزها'}
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
                        افزودن مجوز جدید
                    </Button>
                </div>
                <Table
                    rowKey="id"
                    columns={columns}
                    dataSource={licenses}
                    loading={isLoading}
                    pagination={false}
                    scroll={{ x: true }}
                    locale={{ emptyText: 'مجوزی ثبت نشده است' }}
                />
            </Modal>
            <Modal
                title="افزودن مجوز جدید"
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
                    <Form.Item name="licenseNumber" label="شماره مجوز">
                        <Input />
                    </Form.Item>
                    <Form.Item name="holderName" label="نام دارنده مجوز">
                        <Input />
                    </Form.Item>
                    <Form.Item
                        name="issueDate"
                        label="تاریخ صدور"
                        rules={[{ required: true, message: 'تاریخ صدور مجوز الزامی است' }]}
                    >
                        <SolarDatePicker />
                    </Form.Item>
                    <Form.Item name="expiryDate" label="تاریخ انقضا">
                        <SolarDatePicker />
                    </Form.Item>
                    <Form.Item name="licenseFilePath" label="مسیر فایل مجوز">
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </>
    );
}
