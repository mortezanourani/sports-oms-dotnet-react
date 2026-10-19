import { useState } from 'react';
import { Button, Space, Typography, message, Modal, Form, Input, Upload, List, Select } from 'antd';
import { PlusOutlined, DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import { useProvinces, useCreateProvince } from '../../../hooks/useLocations';
import { useImportLocations } from '../../../hooks/useLocations';
import { downloadLocationsImportTemplate } from '../../../api/locations';
import CitiesPanel from './CitiesPanel';

const { Title } = Typography;

export default function LocationsPage() {
    const { data: provinces } = useProvinces();
    const [modalOpen, setModalOpen] = useState(false);
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const [form] = Form.useForm();

    const createMutation = useCreateProvince();
    const importMutation = useImportLocations();

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadLocationsImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'locations-import-template.xlsx';
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) {
            message.error(error.response?.data?.detail || 'دانلود فایل نمونه با خطا مواجه شد');
        }
    };

    const handleImport = async () => {
        if (!selectedFile) return;
        try {
            const result = await importMutation.mutateAsync(selectedFile);
            setImportModalOpen(false);
            setFileList([]);
            setSelectedFile(null);
            setImportResult(result);
        } catch (error) {
            message.error(error.response?.data?.detail || 'وارد کردن مکان‌ها با خطا مواجه شد');
        }
    };

    const openCreate = () => {
        form.resetFields();
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        try {
            await createMutation.mutateAsync(values);
            message.success('با موفقیت ذخیره شد');
            setModalOpen(false);
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>استان‌ها و شهرها</Title>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
                <Select
                    allowClear
                    placeholder="فیلتر بر اساس استان"
                    style={{ width: 240 }}
                    value={selectedProvinceId || undefined}
                    onChange={(value) => setSelectedProvinceId(value || null)}
                    options={provinces?.map((province) => ({
                        value: province.id,
                        label: province.name,
                    }))}
                />
                <Space wrap>
                    <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
                        وارد کردن مکان‌ها
                    </Button>
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        افزودن استان
                    </Button>
                </Space>
            </div>

            <CitiesPanel
                key={selectedProvinceId || 'all-provinces'}
                provinceId={selectedProvinceId}
                provinces={provinces}
            />

            <Modal
                title="افزودن استان جدید"
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish}>
                    <Form.Item name="name" label="نام استان" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title="وارد کردن استان‌ها و شهرها"
                open={importModalOpen}
                onCancel={() => {
                    if (importMutation.isPending) return;
                    setImportModalOpen(false);
                    setFileList([]);
                    setSelectedFile(null);
                }}
                onOk={handleImport}
                okText="بارگذاری"
                cancelText="انصراف"
                okButtonProps={{ disabled: !selectedFile }}
                confirmLoading={importMutation.isPending}
                closable={!importMutation.isPending}
                maskClosable={!importMutation.isPending}
            >
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    <Button icon={<DownloadOutlined />} onClick={handleDownloadTemplate}>
                        دانلود فایل قالب
                    </Button>
                    <Upload
                        accept=".xlsx"
                        fileList={fileList}
                        maxCount={1}
                        disabled={importMutation.isPending}
                        beforeUpload={() => false}
                        onChange={({ fileList: nextFileList }) => {
                            const next = nextFileList.slice(-1);
                            setFileList(next);
                            setSelectedFile(next[0]?.originFileObj || null);
                        }}
                        onRemove={() => {
                            setSelectedFile(null);
                            setFileList([]);
                        }}
                    >
                        <Button icon={<UploadOutlined />} disabled={importMutation.isPending}>
                            انتخاب فایل Excel
                        </Button>
                    </Upload>
                </Space>
            </Modal>

            <Modal
                title="نتیجه وارد کردن مکان‌ها"
                open={Boolean(importResult)}
                onCancel={() => setImportResult(null)}
                footer={null}
            >
                {importResult && (
                    <>
                        <p>استان ایجادشده: {importResult.createdProvinces}</p>
                        <p>شهرستان ایجادشده: {importResult.createdCities}</p>
                        <p>رکورد موجود و ردشده: {importResult.skippedExisting}</p>
                        <p>ردشده به دلیل نام استان خالی: {importResult.skippedMissingProvince}</p>
                        <p>ردشده به دلیل نام استان نامعتبر: {importResult.skippedInvalidProvince}</p>
                        <p>ناموفق: {importResult.failed}</p>
                        {importResult.errors.length > 0 && (
                            <List
                                size="small"
                                bordered
                                dataSource={importResult.errors}
                                renderItem={(error) => (
                                    <List.Item>ردیف {error.rowNumber}: {error.reason}</List.Item>
                                )}
                                style={{ maxHeight: 280, overflowY: 'auto' }}
                            />
                        )}
                    </>
                )}
            </Modal>
        </div>
    );
}
