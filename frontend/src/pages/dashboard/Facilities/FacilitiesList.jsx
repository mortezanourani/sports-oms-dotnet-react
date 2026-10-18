import { useState } from 'react';
import { Button, List, message, Modal, Select, Space, Typography, Upload } from 'antd';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import {
    downloadMsyGovFacilitiesImportTemplate,
    downloadPrivateFacilitiesImportTemplate,
} from '../../../api/facilities';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import { useImportMsyGovFacilities, useImportPrivateFacilities } from '../../../hooks/useFacilities';
import FacilitiesTable from './FacilitiesTable';

const { Title } = Typography;

export default function FacilitiesList({ facilityTypes, title }) {
    const geo = useGeoSections();
    const { role } = useAuth();
    const [page, setPage] = useState(1);
    const [msyPage, setMsyPage] = useState(1);
    const [govPage, setGovPage] = useState(1);
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [fileList, setFileList] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [importResult, setImportResult] = useState(null);
    const [importType, setImportType] = useState(null);
    const privateImportMutation = useImportPrivateFacilities();
    const msyGovImportMutation = useImportMsyGovFacilities();
    const canImportPrivate = facilityTypes.includes('Private') &&
        ['Admin', 'Manager', 'M5Manager', 'M5Officer'].includes(role);
    const canImportMsyGov = facilityTypes.some((type) => ['Msy', 'Gov'].includes(type)) &&
        ['Admin', 'Manager', 'M88Manager', 'M88Officer'].includes(role);
    const isImportPending = privateImportMutation.isPending || msyGovImportMutation.isPending;
    const hasSeparateMsyGovLists = facilityTypes.includes('Msy') && facilityTypes.includes('Gov');

    const handleDownloadTemplate = async () => {
        try {
            const blob = importType === 'private'
                ? await downloadPrivateFacilitiesImportTemplate()
                : await downloadMsyGovFacilitiesImportTemplate();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = importType === 'private'
                ? 'private-facilities-import-template.xlsx'
                : 'msy-gov-facilities-import-template.xlsx';
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
            const result = importType === 'private'
                ? await privateImportMutation.mutateAsync(selectedFile)
                : await msyGovImportMutation.mutateAsync(selectedFile);
            setImportModalOpen(false);
            setFileList([]);
            setSelectedFile(null);
            setImportResult({ ...result, type: importType });
        } catch (error) {
            message.error(error.response?.data?.detail || 'وارد کردن اماکن با خطا مواجه شد');
        }
    };

    const openImportModal = (type) => {
        setImportType(type);
        setImportModalOpen(true);
    };

    const closeImportModal = () => {
        setImportModalOpen(false);
        setFileList([]);
        setSelectedFile(null);
        setImportType(null);
    };

    const handleProvinceChange = (provinceId) => {
        geo.setSelectedProvinceId(provinceId);
        geo.setSelectedCityId(null);
        setPage(1);
        setMsyPage(1);
        setGovPage(1);
    };

    const handleCityChange = (cityId) => {
        geo.setSelectedCityId(cityId);
        setPage(1);
        setMsyPage(1);
        setGovPage(1);
    };

    const locationFilters = (facilityTypeFilter) => (
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
    );

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>{title}</Title>
            {hasSeparateMsyGovLists ? (
                <>
                    <Title level={5}>اماکن وزارت ورزش و جوانان (MSY)</Title>
                    <FacilitiesTable
                        filters={{
                            provinceId: geo.selectedProvinceId,
                            cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
                        }}
                        facilityTypes={['Msy']}
                        page={msyPage}
                        onPageChange={setMsyPage}
                        hideFacilityTypeColumn
                        creationFacilityTypes={['Msy', 'Gov']}
                        renderActions={canImportMsyGov && (
                            <Button icon={<UploadOutlined />} onClick={() => openImportModal('msy-gov')}>
                                وارد کردن اماکن MSY و دولتی
                            </Button>
                        )}
                        renderLocationFilters={locationFilters}
                    />
                    <div style={{ marginTop: 32 }}>
                        <Title level={5}>اماکن دولتی سایر ارگان‌ها (GOV)</Title>
                        <FacilitiesTable
                            filters={{
                                provinceId: geo.selectedProvinceId,
                                cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
                            }}
                            facilityTypes={['Gov']}
                            page={govPage}
                            onPageChange={setGovPage}
                            hideFacilityTypeColumn
                            showCreateButton={false}
                        />
                    </div>
                </>
            ) : (
                <FacilitiesTable
                    filters={{
                        provinceId: geo.selectedProvinceId,
                        cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId,
                    }}
                    facilityTypes={facilityTypes}
                    page={page}
                    onPageChange={setPage}
                    renderActions={(canImportPrivate || canImportMsyGov) && (
                        <>
                            {canImportPrivate && (
                                <Button icon={<UploadOutlined />} onClick={() => openImportModal('private')}>
                                    وارد کردن اماکن خصوصی
                                </Button>
                            )}
                            {canImportMsyGov && (
                                <Button icon={<UploadOutlined />} onClick={() => openImportModal('msy-gov')}>
                                    وارد کردن اماکن MSY و دولتی
                                </Button>
                            )}
                        </>
                    )}
                    renderLocationFilters={locationFilters}
                />
            )}
            {(canImportPrivate || canImportMsyGov) && (
                <>
                    <Modal
                        title={importType === 'private'
                            ? 'وارد کردن اماکن خصوصی و مجوزها'
                            : 'وارد کردن اماکن MSY و دولتی'}
                        open={importModalOpen}
                        onCancel={() => {
                            if (isImportPending) return;
                            closeImportModal();
                        }}
                        onOk={handleImport}
                        okText="بارگذاری"
                        cancelText="انصراف"
                        okButtonProps={{ disabled: !selectedFile }}
                        confirmLoading={isImportPending}
                        closable={!isImportPending}
                        maskClosable={!isImportPending}
                    >
                        <Space direction="vertical" style={{ width: '100%' }} size="middle">
                            <Button icon={<DownloadOutlined />} onClick={handleDownloadTemplate}>
                                دانلود فایل قالب
                            </Button>
                            <Upload
                                accept=".xlsx"
                                fileList={fileList}
                                maxCount={1}
                                disabled={isImportPending}
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
                                <Button icon={<UploadOutlined />} disabled={isImportPending}>
                                    انتخاب فایل Excel
                                </Button>
                            </Upload>
                        </Space>
                    </Modal>
                    <Modal
                        title={importResult?.type === 'private'
                            ? 'نتیجه وارد کردن اماکن خصوصی و مجوزها'
                            : 'نتیجه وارد کردن اماکن MSY و دولتی'}
                        open={Boolean(importResult)}
                        onCancel={() => setImportResult(null)}
                        footer={null}
                    >
                        {importResult && (
                            <>
                                <p>اماکن ایجادشده: {importResult.createdFacilities}</p>
                                <p>اماکن از قبل موجود: {importResult.existingFacilities}</p>
                                {importResult.type === 'private' ? (
                                    <>
                                        <p>مجوزهای ایجادشده: {importResult.createdLicenses}</p>
                                        <p>مجوزهای تکراری: {importResult.skippedExistingLicenses}</p>
                                        <p>اماکن ردشده به دلیل داده نامعتبر: {importResult.skippedInvalidFacilities}</p>
                                        <p>مجوزهای نامعتبر و ثبت‌نشده: {importResult.skippedInvalidLicenses}</p>
                                    </>
                                ) : (
                                    <>
                                        <p>قراردادهای ایجادشده: {importResult.createdContracts}</p>
                                        <p>قراردادهای تکراری: {importResult.skippedExistingContracts}</p>
                                        <p>ردشده به دلیل داده نامعتبر: {importResult.skippedInvalid}</p>
                                        <p>قراردادهای نامعتبر یا غیرمجاز و ثبت‌نشده: {importResult.skippedInvalidContracts}</p>
                                    </>
                                )}
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
