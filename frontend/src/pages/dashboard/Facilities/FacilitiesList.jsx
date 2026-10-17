import { useState } from 'react';
import { Button, List, message, Modal, Select, Space, Typography, Upload } from 'antd';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import { downloadPrivateFacilitiesImportTemplate } from '../../../api/facilities';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import { useImportPrivateFacilities } from '../../../hooks/useFacilities';
import FacilitiesTable from './FacilitiesTable';

const { Title } = Typography;

export default function FacilitiesList({ facilityTypes, title }) {
    const geo = useGeoSections();
    const { role } = useAuth();
    const [page, setPage] = useState(1);
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);
    const importMutation = useImportPrivateFacilities();
    const canImport = facilityTypes.includes('Private') &&
        ['Admin', 'Manager', 'M5Manager', 'M5Officer'].includes(role);

    const handleDownloadTemplate = async () => {
        try {
            const blob = await downloadPrivateFacilitiesImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'private-facilities-import-template.xlsx';
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
            message.error(error.response?.data?.detail || 'وارد کردن اماکن خصوصی با خطا مواجه شد');
        }
    };

    const handleProvinceChange = (provinceId) => {
        geo.setSelectedProvinceId(provinceId);
        geo.setSelectedCityId(null);
        setPage(1);
    };

    const handleCityChange = (cityId) => {
        geo.setSelectedCityId(cityId);
        setPage(1);
    };

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>{title}</Title>
            <FacilitiesTable
                filters={{
                    provinceId: geo.selectedProvinceId,
                    cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
                }}
                facilityTypes={facilityTypes}
                page={page}
                onPageChange={setPage}
                renderActions={canImport && (
                    <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
                        وارد کردن اماکن خصوصی
                    </Button>
                )}
                renderLocationFilters={(facilityTypeFilter) => (
                    <Space wrap>
                        {geo.auth.scopeLevel !== 'City' && (
                            <>
                                {geo.auth.scopeLevel === 'Mastery' && (
                                    <Select
                                        placeholder="فیلتر بر اساس استان"
                                        allowClear
                                        style={{ width: 220 }}
                                        value={geo.selectedProvinceId}
                                        onChange={handleProvinceChange}
                                        options={geo.provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
                                    />
                                )}
                                <Select
                                    placeholder={geo.selectedProvinceId ? 'فیلتر بر اساس شهر' : 'ابتدا استان را انتخاب کنید'}
                                    allowClear
                                    style={{ width: 220 }}
                                    disabled={!geo.selectedProvinceId}
                                    value={geo.selectedCityId}
                                    onChange={handleCityChange}
                                    options={geo.cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                                />
                            </>
                        )}
                        {facilityTypeFilter}
                    </Space>
                )}
            />
            {canImport && (
                <>
                    <Modal
                        title="وارد کردن اماکن خصوصی و مجوزها"
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
                        title="نتیجه وارد کردن اماکن خصوصی و مجوزها"
                        open={Boolean(importResult)}
                        onCancel={() => setImportResult(null)}
                        footer={null}
                    >
                        {importResult && (
                            <>
                                <p>اماکن ایجادشده: {importResult.createdFacilities}</p>
                                <p>اماکن از قبل موجود: {importResult.existingFacilities}</p>
                                <p>مجوزهای ایجادشده: {importResult.createdLicenses}</p>
                                <p>مجوزهای تکراری: {importResult.skippedExistingLicenses}</p>
                                <p>اماکن ردشده به دلیل داده نامعتبر: {importResult.skippedInvalidFacilities}</p>
                                <p>مجوزهای نامعتبر و ثبت‌نشده: {importResult.skippedInvalidLicenses}</p>
                                <p>ردشده به دلیل خارج بودن از محدوده دسترسی: {importResult.skippedOutOfScope}</p>
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
                </>
            )}
        </div>
    );
}
