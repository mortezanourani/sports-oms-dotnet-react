import { useEffect, useState } from 'react';
import { Table, Button, Space, Popconfirm, message, Select, Modal, Form, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import {
    useFacilities, useFacility, useCreateFacility, useUpdateFacility, useDeleteFacility,
} from '../../../hooks/useFacilities';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { useAuth } from '../../../hooks/useAuth';
import ContractHistoryModal from './ContractHistoryModal';

const FACILITY_TYPE_LABELS = { Msy: 'دولتی وزارت ورزش', Private: 'باشگاه خصوصی', Gov: 'دولتی سایر ارگان ها' };

export default function FacilitiesTable({
    filters,
    facilityTypes,
    page,
    onPageChange,
    renderLocationFilters,
    renderActions,
    hideFacilityTypeColumn = false,
    showCreateButton = true,
    creationFacilityTypes = facilityTypes,
}) {
    const [facilityType, setFacilityType] = useState(undefined);
    const [modalOpen, setModalOpen] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [contractsFacility, setContractsFacility] = useState(null);
    const [form] = Form.useForm();
    const { role } = useAuth();
    const selectedProvinceId = Form.useWatch('provinceId', form);
    const defaultFacilityType = creationFacilityTypes.length === 1 ? creationFacilityTypes[0] : undefined;
    const selectedFacilityType = defaultFacilityType || facilityType;
    const canDeleteFacility = ['Admin', 'Manager', 'M5Manager', 'M88Manager'].includes(role);
    const canViewContracts = ['Admin', 'Manager', 'M88Manager', 'M88Officer'].includes(role);

    const { data, isLoading } = useFacilities({
        page,
        pageSize: 10,
        facilityTypes: selectedFacilityType || facilityTypes.join(','),
        ...filters,
    });
    const { data: editingFacility, isFetching: isFetchingEditingFacility } = useFacility(editingId);
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const createMutation = useCreateFacility();
    const updateMutation = useUpdateFacility();
    const deleteMutation = useDeleteFacility();

    // Populates the form once the full facility record has loaded — only
    // relevant in edit mode, since create mode has nothing to pre-fill.
    useEffect(() => {
        if (editingId && editingFacility) {
            form.setFieldsValue(editingFacility);
        }
    }, [editingId, editingFacility, form]);

    const openCreateModal = () => {
        setEditingId(null);
        form.resetFields();
        if (defaultFacilityType) form.setFieldValue('facilityType', defaultFacilityType);
        setModalOpen(true);
    };

    const openEditModal = (record) => {
        setEditingId(record.id);
        setModalOpen(true);
        // form.setFieldsValue happens in the useEffect above, once the
        // detailed record has actually finished loading — not here, since
        // record from the table row is the lighter list-shape, not
        // guaranteed to have every field the form needs.
    };

    const handleCancel = () => {
        setModalOpen(false);
        form.resetFields();
    };

    const handleFinish = async (values) => {
        try {
            const payload = { ...values };
            delete payload.provinceId;
            if (editingId) {
                await updateMutation.mutateAsync({ id: editingId, payload });
            } else {
                await createMutation.mutateAsync(payload);
            }
            message.success('با موفقیت ذخیره شد');
            setModalOpen(false);
            form.resetFields();
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const handleDelete = async (id) => {
        try {
            await deleteMutation.mutateAsync(id);
            message.success('با موفقیت حذف شد');
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const columns = [
        { title: 'نام', dataIndex: 'name', key: 'name' },
        ...(!hideFacilityTypeColumn
            ? [{ title: 'نوع', dataIndex: 'facilityType', key: 'facilityType', render: (v) => FACILITY_TYPE_LABELS[v] || v }]
            : []),
        { title: 'استان', dataIndex: 'provinceName', key: 'provinceName' },
        { title: 'شهر', dataIndex: 'cityName', key: 'cityName' },
        { title: 'آدرس', dataIndex: 'address', key: 'address' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    {record.facilityType === 'Msy' && canViewContracts && (
                        <Button
                            icon={<HistoryOutlined />}
                            size="small"
                            onClick={() => setContractsFacility(record)}
                        >
                            سوابق قراردادها
                        </Button>
                    )}
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEditModal(record)} />
                    {canDeleteFacility && (
                        <Popconfirm title="آیا از حذف این مورد مطمئن هستید؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                            <Button icon={<DeleteOutlined />} size="small" danger />
                        </Popconfirm>
                    )}
                </Space>
            ),
        },
    ];

    const facilityTypeFilter = facilityTypes.length > 1 && (
        <Select
            placeholder="فیلتر بر اساس نوع"
            allowClear
            style={{ width: 200 }}
            value={facilityType}
            onChange={(value) => {
                setFacilityType(value);
                onPageChange(1);
            }}
            options={facilityTypes.map((type) => ({ value: type, label: FACILITY_TYPE_LABELS[type] }))}
        />
    );

    return (
        <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, marginBottom: 16 }}>
                <div>{renderLocationFilters?.(facilityTypeFilter) || facilityTypeFilter}</div>
                <Space>
                    {renderActions}
                    {showCreateButton && (
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>افزودن مکان جدید</Button>
                    )}
                </Space>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={data?.items}
                loading={isLoading}
                pagination={{ current: page, pageSize: 10, total: data?.count, onChange: onPageChange }}
            />

            <Modal
                title={editingId ? 'ویرایش مکان' : 'افزودن مکان جدید'}
                open={modalOpen}
                onCancel={handleCancel}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditingFacility}>
                    <Form.Item name="facilityType" label="نوع مکان" rules={[{ required: true }]}>
                        <Select
                            options={creationFacilityTypes.map((type) => ({ value: type, label: FACILITY_TYPE_LABELS[type] }))}
                            disabled={Boolean(editingId) || creationFacilityTypes.length === 1}
                        />
                    </Form.Item>
                    <Form.Item name="name" label="نام" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="provinceId" label="استان">
                        <Select
                            placeholder="ابتدا استان را انتخاب کنید"
                            options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                            onChange={() => {
                                form.setFieldValue('cityId', undefined);
                            }}
                        />
                    </Form.Item>
                    <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                        <Select
                            placeholder="شهر را انتخاب کنید"
                            disabled={!selectedProvinceId}
                            options={cities?.map((c) => ({ value: c.id, label: c.name }))}
                        />
                    </Form.Item>
                    <Form.Item name="address" label="آدرس" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="sportType" label="نوع ورزش">
                        <Input />
                    </Form.Item>
                    <Form.Item name="ownerName" label="نام مالک">
                        <Input />
                    </Form.Item>
                    <Form.Item name="ownerContact" label="تماس مالک">
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
            {facilityTypes.includes('Msy') && (
                <ContractHistoryModal
                    facility={contractsFacility}
                    open={Boolean(contractsFacility)}
                    onClose={() => setContractsFacility(null)}
                />
            )}
        </div>
    );
}
