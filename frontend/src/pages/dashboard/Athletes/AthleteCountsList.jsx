import { useState } from 'react';
import { Select, Typography, Button, Space, Upload, Modal, List, message } from 'antd';
import { DownloadOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { downloadAthleteCountsImportTemplate } from '../../../api/athleteCounts';
import { useAthleteCountSummaryFilters, useImportAthleteCounts } from '../../../hooks/useAthleteCounts';
import { useAuth } from '../../../hooks/useAuth';
import { useGeoSections } from '../../../hooks/useGeoSections';
import { PERSIAN_MONTHS } from '../../../constants/labels';
import AthleteCountsTable from './AthleteCountsTable';

const { Title } = Typography;

export default function AthleteCountsList() {
  const geo = useGeoSections();
  const { role } = useAuth();
  const canManageCounts = ['Admin', 'Manager', 'InsuranceOfficer'].includes(role);
  const canImport = ['Admin', 'Manager', 'InsuranceOfficer'].includes(role);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [fileList, setFileList] = useState([]);
  const [selectedFile, setSelectedFile] = useState(null);
  const [importResult, setImportResult] = useState(null);
  const [selectedYear, setSelectedYear] = useState(null);
  const [selectedMonth, setSelectedMonth] = useState(null);
  const importMutation = useImportAthleteCounts();
  const summaryLocationFilters = {
    ...(geo.selectedProvinceId ? { provinceId: geo.selectedProvinceId } : {}),
    ...((geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId)
      ? { cityId: geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId }
      : {}),
  };
  const { data: summaryFilterOptions } = useAthleteCountSummaryFilters({
    ...summaryLocationFilters,
    ...(selectedYear ? { year: selectedYear } : {}),
  });

  const handleDownloadTemplate = async () => {
    try {
      const blob = await downloadAthleteCountsImportTemplate();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'athlete-counts-import-template.xlsx';
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
      message.error(error.response?.data?.detail || 'وارد کردن آمار ورزشکاران با خطا مواجه شد');
    }
  };

  const handleProvinceChange = (provinceId) => {
    geo.setSelectedProvinceId(provinceId || null);
    geo.setSelectedCityId(null);
    setSelectedYear(null);
    setSelectedMonth(null);
  };

  const handleCityChange = (cityId) => {
    geo.setSelectedCityId(cityId || null);
    setSelectedYear(null);
    setSelectedMonth(null);
  };

  const handleYearChange = (year) => {
    setSelectedYear(year || null);
    setSelectedMonth(null);
  };

  const filters = {
    ...summaryLocationFilters,
    ...(selectedYear ? { year: selectedYear } : {}),
    ...(selectedMonth ? { month: selectedMonth } : {}),
  };

  const renderToolbar = ({ onAdd }) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
      <Space wrap>
        <Select
          allowClear
          placeholder="فیلتر بر اساس استان"
          style={{ width: 220 }}
          disabled={geo.provinceLocked}
          value={geo.selectedProvinceId || undefined}
          onChange={handleProvinceChange}
          options={geo.provinceOptions?.map((province) => ({ value: province.id, label: province.name }))}
        />
        <Select
          allowClear
          placeholder={geo.selectedProvinceId ? 'فیلتر بر اساس شهر' : 'ابتدا استان را انتخاب کنید'}
          style={{ width: 220 }}
          disabled={!geo.selectedProvinceId || geo.cityLocked}
          value={(geo.cityLocked ? geo.auth.scopeCityId : geo.selectedCityId) || undefined}
          onChange={handleCityChange}
          options={geo.cityOptions?.map((city) => ({ value: city.id, label: city.name }))}
        />
        <Select
          allowClear
          placeholder="فیلتر بر اساس سال"
          style={{ width: 150 }}
          value={selectedYear || undefined}
          onChange={handleYearChange}
          options={summaryFilterOptions?.years?.map((year) => ({ value: year, label: year }))}
        />
        <Select
          allowClear
          placeholder={selectedYear ? 'فیلتر بر اساس ماه' : 'ابتدا سال را انتخاب کنید'}
          style={{ width: 170 }}
          disabled={!selectedYear}
          value={selectedMonth || undefined}
          onChange={(month) => setSelectedMonth(month || null)}
          options={summaryFilterOptions?.months?.map((month) => ({
            value: month,
            label: PERSIAN_MONTHS[month - 1],
          }))}
        />
      </Space>
      <Space wrap>
        {canManageCounts && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onAdd}>
            ثبت آمار جدید
          </Button>
        )}
        {canImport && (
          <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
            وارد کردن آمار
          </Button>
        )}
      </Space>
    </div>
  );

  return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>آمار ورزشکاران</Title>
        </div>
        <AthleteCountsTable
          filters={filters}
          renderToolbar={renderToolbar}
          canEdit={canManageCounts && Boolean(
            filters.provinceId && filters.cityId && filters.year && filters.month
          )}
        />
        <Modal
          title="وارد کردن آمار ورزشکاران"
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
          title="نتیجه وارد کردن آمار"
          open={Boolean(importResult)}
          onCancel={() => setImportResult(null)}
          footer={null}
        >
          {importResult && (
            <>
              <p>رکوردهای ایجادشده: {importResult.created}</p>
              <p>ردشده به دلیل داده نامعتبر: {importResult.skippedInvalid}</p>
              <p>ردشده به دلیل خارج بودن از محدوده دسترسی: {importResult.skippedOutOfScope}</p>
              <p>ردشده به دلیل وجود رکورد تکراری: {importResult.skippedExisting}</p>
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
