import { useEffect, useState } from 'react';
import {
  Alert, Button, Form, List, Modal, Select, Space, Table, Tag, Typography, Upload, message,
} from 'antd';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { useUsers } from '../../../hooks/useUsers';
import {
  useAssignJobPost, useEndJobPostAssignment, useImportJobPostAssignments,
  useJobPostAssignmentPosts, useJobPostAssignments,
} from '../../../hooks/useJobPosts';
import {
  downloadJobPostAssignmentsImportTemplate,
} from '../../../api/jobPosts';
import { formatSolarDate } from '../../../utils/dates';

const { Title, Text } = Typography;

function toDateOnly(value) {
  if (!value) return null;
  const date = value instanceof Date ? value : value.toDate();
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-');
}

export default function JobPostsPanel() {
  const [assignmentForm] = Form.useForm();
  const [endForm] = Form.useForm();
  const [assignmentModalOpen, setAssignmentModalOpen] = useState(false);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [endingAssignment, setEndingAssignment] = useState(null);
  const [userSearch, setUserSearch] = useState('');
  const [userSearchInput, setUserSearchInput] = useState('');
  const [selectedUserId, setSelectedUserId] = useState();
  const [selectedJobPostId, setSelectedJobPostId] = useState();
  const [fileList, setFileList] = useState([]);
  const [selectedFile, setSelectedFile] = useState(null);
  const [importResult, setImportResult] = useState(null);

  const { data: posts = [], isLoading: postsLoading, error: postsError } = useJobPostAssignmentPosts();
  const { data: usersData, isLoading: usersLoading } = useUsers({
    page: 1,
    pageSize: 100,
    search: userSearch,
  });
  const {
    data: assignments = [],
    isLoading: assignmentsLoading,
    error: assignmentsError,
  } = useJobPostAssignments({
    ...(selectedUserId ? { userId: selectedUserId } : {}),
    ...(selectedJobPostId ? { jobPostId: selectedJobPostId } : {}),
  });

  const assignMutation = useAssignJobPost();
  const endMutation = useEndJobPostAssignment();
  const importMutation = useImportJobPostAssignments();

  useEffect(() => {
    const timer = window.setTimeout(() => setUserSearch(userSearchInput.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [userSearchInput]);

  const userOptions = (usersData?.items || []).map((user) => ({
    value: user.id,
    label: `${user.firstName} ${user.lastName} (${user.username})`.trim(),
  }));
  const postOptions = posts.map((post) => ({
    value: post.id,
    label: `${post.title} - ${post.jobPostUniqueId}`,
  }));

  const openAssignmentModal = () => {
    assignmentForm.resetFields();
    setUserSearchInput('');
    setUserSearch('');
    setAssignmentModalOpen(true);
  };

  const assignUser = async (values) => {
    try {
      await assignMutation.mutateAsync({
        userId: values.userId,
        jobPostId: values.jobPostId,
        startDate: toDateOnly(values.startDate),
        endDate: toDateOnly(values.endDate),
      });
      message.success('تخصیص پست با موفقیت ثبت شد');
      setAssignmentModalOpen(false);
      assignmentForm.resetFields();
    } catch (error) {
      message.error(error.response?.data?.detail || 'تخصیص پست با خطا مواجه شد');
    }
  };

  const endAssignment = async (values) => {
    try {
      await endMutation.mutateAsync({
        id: endingAssignment.id,
        endDate: toDateOnly(values.endDate),
      });
      message.success('تخصیص پایان یافت');
      setEndingAssignment(null);
      endForm.resetFields();
    } catch (error) {
      message.error(error.response?.data?.detail || 'پایان تخصیص با خطا مواجه شد');
    }
  };

  const downloadTemplate = async () => {
    try {
      const blob = await downloadJobPostAssignmentsImportTemplate();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'job-post-assignments-import-template.xlsx';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) {
      message.error(error.response?.data?.detail || 'دانلود فایل قالب با خطا مواجه شد');
    }
  };

  const importAssignments = async () => {
    if (!selectedFile) return;
    try {
      const result = await importMutation.mutateAsync(selectedFile);
      setImportModalOpen(false);
      setFileList([]);
      setSelectedFile(null);
      setImportResult(result);
    } catch (error) {
      message.error(error.response?.data?.detail || 'وارد کردن تخصیص‌ها با خطا مواجه شد');
    }
  };

  const columns = [
    {
      title: 'کاربر',
      render: (_, item) => `${item.userFirstName} ${item.userLastName} (${item.username})`.trim(),
    },
    {
      title: 'پست شغلی',
      render: (_, item) => `${item.jobPostTitle} - ${item.jobPostUniqueId}`,
    },
    { title: 'از تاریخ', dataIndex: 'startDate', render: formatSolarDate },
    {
      title: 'تا تاریخ',
      dataIndex: 'endDate',
      render: (date) => date ? formatSolarDate(date) : <Tag color="green">در حال حاضر</Tag>,
    },
    {
      title: 'عملیات',
      render: (_, item) => !item.endDate && (
        <Button
          danger
          onClick={() => {
            endForm.resetFields();
            setEndingAssignment(item);
          }}
        >
          پایان تخصیص
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      {(postsError || assignmentsError) && (
        <Alert type="error" message="دریافت پست‌ها یا سوابق تخصیص با خطا مواجه شد" />
      )}
      <section>
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          marginBottom: 12,
          flexWrap: 'wrap',
        }}
        >
          <Title level={5} style={{ margin: 0 }}>سوابق تخصیص پست</Title>
          <Space wrap>
            <Button icon={<UploadOutlined />} onClick={() => setImportModalOpen(true)}>
              وارد کردن از Excel
            </Button>
            <Button type="primary" onClick={openAssignmentModal}>
              تخصیص پست به کاربر
            </Button>
          </Space>
        </div>
        <Space wrap style={{ marginBottom: 12 }}>
          <Select
            allowClear
            showSearch
            filterOption={false}
            onSearch={setUserSearchInput}
            placeholder="همه کاربران"
            style={{ width: 280 }}
            loading={usersLoading}
            value={selectedUserId}
            onChange={setSelectedUserId}
            options={userOptions}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="همه پست‌ها"
            style={{ width: 280 }}
            loading={postsLoading}
            value={selectedJobPostId}
            onChange={setSelectedJobPostId}
            options={postOptions}
          />
        </Space>
        <Table
          rowKey="id"
          columns={columns}
          dataSource={assignments}
          loading={assignmentsLoading}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: 'سابقه تخصیصی ثبت نشده است' }}
          scroll={{ x: true }}
        />
      </section>

      <Modal
        title="تخصیص پست شغلی"
        open={assignmentModalOpen}
        onCancel={() => setAssignmentModalOpen(false)}
        onOk={() => assignmentForm.submit()}
        confirmLoading={assignMutation.isPending}
        okText="ثبت تخصیص"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={assignmentForm} layout="vertical" onFinish={assignUser}>
          <Form.Item name="userId" label="کاربر" rules={[{ required: true, message: 'کاربر را انتخاب کنید' }]}>
            <Select
              showSearch
              filterOption={false}
              onSearch={setUserSearchInput}
              options={userOptions}
              placeholder="جستجوی نام یا نام کاربری"
              notFoundContent="کاربری یافت نشد"
            />
          </Form.Item>
          <Form.Item
            name="jobPostId"
            label="پست شغلی"
            rules={[{ required: true, message: 'پست شغلی را انتخاب کنید' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              options={postOptions}
              loading={postsLoading}
            />
          </Form.Item>
          <Form.Item name="startDate" label="تاریخ شروع (اختیاری)">
            <SolarDatePicker placeholder="انتخاب تاریخ شروع" />
          </Form.Item>
          <Form.Item name="endDate" label="تاریخ پایان (اختیاری)">
            <SolarDatePicker placeholder="انتخاب تاریخ پایان" />
          </Form.Item>
          <Text type="secondary">
            تاریخ پایان شامل همان روز است. خالی بودن تاریخ شروع یعنی نامشخص و خالی بودن تاریخ پایان یعنی تخصیص جاری.
          </Text>
        </Form>
      </Modal>

      <Modal
        title={`پایان تخصیص ${endingAssignment?.jobPostTitle || ''}`}
        open={Boolean(endingAssignment)}
        onCancel={() => setEndingAssignment(null)}
        onOk={() => endForm.submit()}
        confirmLoading={endMutation.isPending}
        okText="ثبت پایان"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={endForm} layout="vertical" onFinish={endAssignment}>
          <Form.Item
            name="endDate"
            label="تاریخ پایان"
            rules={[{ required: true, message: 'تاریخ پایان الزامی است' }]}
          >
            <SolarDatePicker placeholder="انتخاب تاریخ پایان" />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="وارد کردن تخصیص پست‌ها"
        open={importModalOpen}
        onCancel={() => {
          if (importMutation.isPending) return;
          setImportModalOpen(false);
          setFileList([]);
          setSelectedFile(null);
        }}
        onOk={importAssignments}
        okText="بارگذاری"
        cancelText="انصراف"
        okButtonProps={{ disabled: !selectedFile }}
        confirmLoading={importMutation.isPending}
        closable={!importMutation.isPending}
        maskClosable={!importMutation.isPending}
      >
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          <Text>
            تاریخ‌های خالی به معنی تاریخ شروع نامشخص و تخصیص جاری است. برای تخصیص جاری تکراری یا بازه هم‌پوشان، ردیف رد می‌شود.
          </Text>
          <Button icon={<DownloadOutlined />} onClick={downloadTemplate}>
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
        title="نتیجه وارد کردن تخصیص‌ها"
        open={Boolean(importResult)}
        onCancel={() => setImportResult(null)}
        footer={null}
      >
        {importResult && (
          <>
            <p>ایجادشده: {importResult.created}</p>
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
    </Space>
  );
}
