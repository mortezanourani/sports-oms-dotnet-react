import { useEffect, useState } from 'react';
import {
  Alert, Button, Form, Input, Modal, Select, Space, Table, Tag, Typography, message,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { useUsers } from '../../../hooks/useUsers';
import {
  useAssignJobPost, useCreateJobPost, useEndJobPostAssignment, useJobPostAssignments,
  useJobPosts, useUpdateJobPost,
} from '../../../hooks/useJobPosts';
import { useAuth } from '../../../hooks/useAuth';
import { formatSolarDate } from '../../../utils/dates';

const { Title } = Typography;

const placementLabels = {
  Province: 'استانی',
  City: 'شهری',
  Both: 'استانی و شهری',
};

function toDateOnly(value) {
  if (!value) return null;
  const date = value instanceof Date ? value : value.toDate();
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-');
}

export default function JobPostsPanel({ variant = 'both' }) {
  const auth = useAuth();
  const [postForm] = Form.useForm();
  const [assignmentForm] = Form.useForm();
  const [endForm] = Form.useForm();
  const assignmentPostId = Form.useWatch('jobPostId', assignmentForm);
  const provinceIdForForm = Form.useWatch('provinceId', assignmentForm);
  const [postModalOpen, setPostModalOpen] = useState(false);
  const [editingPost, setEditingPost] = useState(null);
  const [assignmentModalOpen, setAssignmentModalOpen] = useState(false);
  const [endingAssignment, setEndingAssignment] = useState(null);
  const [searchInput, setSearchInput] = useState('');
  const [userSearch, setUserSearch] = useState('');
  const [selectedPostId, setSelectedPostId] = useState();
  const [selectedProvinceId, setSelectedProvinceId] = useState(auth.scopeProvinceId || undefined);
  const [selectedCityId, setSelectedCityId] = useState(auth.scopeCityId || undefined);

  const { data: posts = [], isLoading: postsLoading, error: postsError } = useJobPosts();
  const { data: provinces = [] } = useProvinces();
  const { data: cities = [] } = useCities(provinceIdForForm);
  const { data: filterCities = [] } = useCities(selectedProvinceId);
  const { data: usersData } = useUsers({
    page: 1,
    pageSize: 100,
    search: userSearch,
  });
  const assignmentFilters = {
    ...(selectedPostId ? { jobPostId: selectedPostId } : {}),
    ...(selectedProvinceId ? { provinceId: selectedProvinceId } : {}),
    ...(selectedCityId ? { cityId: selectedCityId } : {}),
  };
  const {
    data: assignments = [],
    isLoading: assignmentsLoading,
    error: assignmentsError,
  } = useJobPostAssignments(assignmentFilters);

  const createPostMutation = useCreateJobPost();
  const updatePostMutation = useUpdateJobPost();
  const assignMutation = useAssignJobPost();
  const endMutation = useEndJobPostAssignment();

  useEffect(() => {
    const timer = window.setTimeout(() => setUserSearch(searchInput.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const showPostModal = (post = null) => {
    setEditingPost(post);
    postForm.resetFields();
    postForm.setFieldsValue(post
      ? {
          name: post.name,
          placementScope: post.placementScope,
          isActive: post.isActive,
        }
      : { placementScope: 'Both', isActive: true });
    setPostModalOpen(true);
  };

  const savePost = async (values) => {
    const payload = { ...values, name: values.name.trim() };
    try {
      if (editingPost) {
        await updatePostMutation.mutateAsync({ id: editingPost.id, payload });
        message.success('پست شغلی ویرایش شد');
      } else {
        await createPostMutation.mutateAsync(payload);
        message.success('پست شغلی ایجاد شد');
      }
      setPostModalOpen(false);
      postForm.resetFields();
    } catch (error) {
      message.error(error.response?.data?.detail || 'ذخیره پست شغلی با خطا مواجه شد');
    }
  };

  const openAssignmentModal = () => {
    assignmentForm.resetFields();
    assignmentForm.setFieldsValue({
      ...(auth.scopeLevel !== 'Mastery' && { provinceId: auth.scopeProvinceId }),
      ...(auth.scopeLevel === 'City' && { cityId: auth.scopeCityId }),
      startedOn: null,
    });
    setSearchInput('');
    setUserSearch('');
    setAssignmentModalOpen(true);
  };

  const assignUser = async (values) => {
    try {
      const selectedPost = posts.find((post) => post.id === values.jobPostId);
      const cityId = selectedPost?.placementScope === 'Province'
        ? null
        : values.cityId || null;
      await assignMutation.mutateAsync({
        jobPostId: values.jobPostId,
        userId: values.userId,
        provinceId: values.provinceId,
        cityId,
        startedOn: toDateOnly(values.startedOn),
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
        endedOn: toDateOnly(values.endedOn),
      });
      message.success('تخصیص پایان یافت');
      setEndingAssignment(null);
      endForm.resetFields();
    } catch (error) {
      message.error(error.response?.data?.detail || 'پایان تخصیص با خطا مواجه شد');
    }
  };

  const postColumns = [
    { title: 'عنوان پست', dataIndex: 'name' },
    {
      title: 'دامنه جغرافیایی',
      dataIndex: 'placementScope',
      render: (scope) => placementLabels[scope] || scope,
    },
    {
      title: 'وضعیت',
      dataIndex: 'isActive',
      render: (active) => <Tag color={active ? 'green' : 'default'}>{active ? 'فعال' : 'غیرفعال'}</Tag>,
    },
    {
      title: 'عملیات',
      render: (_, post) => <Button onClick={() => showPostModal(post)}>ویرایش</Button>,
    },
  ];

  const assignmentColumns = [
    { title: 'پست شغلی', dataIndex: 'jobPostName' },
    {
      title: 'کاربر',
      render: (_, item) => `${item.userFirstName} ${item.userLastName} (${item.username})`,
    },
    {
      title: 'محدوده',
      render: (_, item) => item.cityName ? `${item.cityName} - ${item.provinceName}` : item.provinceName,
    },
    { title: 'از تاریخ', dataIndex: 'startedOn', render: formatSolarDate },
    {
      title: 'تا تاریخ',
      dataIndex: 'endedOn',
      render: (date) => date ? formatSolarDate(date) : <Tag color="green">در حال حاضر</Tag>,
    },
    {
      title: 'عملیات',
      render: (_, item) => !item.endedOn && (
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

  const provinceOptions = provinces.map((province) => ({
    value: province.id,
    label: province.name,
  }));
  const cityOptions = cities.map((city) => ({ value: city.id, label: city.name }));
  const userOptions = (usersData?.items || []).map((user) => ({
    value: user.id,
    label: `${user.firstName} ${user.lastName} (${user.username})`,
  }));
  const assignablePosts = posts.filter((post) => {
    if (!post.isActive) return false;
    if (auth.scopeLevel === 'City') return post.placementScope !== 'Province';
    return true;
  });
  const selectedAssignmentPost = assignablePosts.find((post) => post.id === assignmentPostId);
  const cityAssignmentRequired = selectedAssignmentPost?.placementScope === 'City' ||
    auth.scopeLevel === 'City';
  const cityAssignmentAllowed = selectedAssignmentPost?.placementScope !== 'Province';

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      {postsError && <Alert type="error" message="دریافت پست‌های شغلی با خطا مواجه شد" />}
      {variant !== 'assignments' && <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <Title level={5} style={{ margin: 0 }}>تعریف پست‌های شغلی</Title>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => showPostModal()}>
            افزودن پست
          </Button>
        </div>
        <Table
          rowKey="id"
          columns={postColumns}
          dataSource={posts}
          loading={postsLoading}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: 'پستی ثبت نشده است' }}
        />
      </section>}

      {variant !== 'definitions' && <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 12, flexWrap: 'wrap' }}>
          <Title level={5} style={{ margin: 0 }}>سوابق تخصیص پست</Title>
          <Button type="primary" onClick={openAssignmentModal} disabled={!assignablePosts.length}>
            تخصیص پست به کاربر
          </Button>
        </div>
        <Space wrap style={{ marginBottom: 12 }}>
          <Select
            allowClear
            placeholder="همه پست‌ها"
            style={{ width: 220 }}
            value={selectedPostId}
            onChange={setSelectedPostId}
            options={posts.map((post) => ({ value: post.id, label: post.name }))}
          />
          {auth.scopeLevel === 'Mastery' && (
            <Select
              allowClear
              placeholder="همه استان‌ها"
              style={{ width: 220 }}
              value={selectedProvinceId}
              onChange={(value) => {
                setSelectedProvinceId(value);
                setSelectedCityId(undefined);
              }}
              options={provinceOptions}
            />
          )}
          {auth.scopeLevel !== 'City' && (
            <Select
              allowClear
              placeholder="همه شهرها"
              style={{ width: 220 }}
              disabled={!selectedProvinceId}
              value={selectedCityId}
              onChange={setSelectedCityId}
              options={filterCities.map((city) => ({ value: city.id, label: city.name }))}
            />
          )}
        </Space>
        {assignmentsError && <Alert type="error" message="دریافت سوابق تخصیص با خطا مواجه شد" />}
        <Table
          rowKey="id"
          columns={assignmentColumns}
          dataSource={assignments}
          loading={assignmentsLoading}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: 'سابقه تخصیصی ثبت نشده است' }}
        />
      </section>}

      <Modal
        title={editingPost ? 'ویرایش پست شغلی' : 'افزودن پست شغلی'}
        open={postModalOpen}
        onCancel={() => setPostModalOpen(false)}
        onOk={() => postForm.submit()}
        confirmLoading={createPostMutation.isPending || updatePostMutation.isPending}
        okText="ذخیره"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={postForm} layout="vertical" onFinish={savePost}>
          <Form.Item name="name" label="عنوان پست" rules={[{ required: true, whitespace: true, max: 160 }]}>
            <Input maxLength={160} />
          </Form.Item>
          <Form.Item name="placementScope" label="دامنه جغرافیایی" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'Province', label: 'استانی' },
                { value: 'City', label: 'شهری' },
                { value: 'Both', label: 'استانی و شهری' },
              ]}
            />
          </Form.Item>
          <Form.Item name="isActive" label="وضعیت" rules={[{ required: true }]}>
            <Select options={[{ value: true, label: 'فعال' }, { value: false, label: 'غیرفعال' }]} />
          </Form.Item>
        </Form>
      </Modal>

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
          <Form.Item name="jobPostId" label="پست شغلی" rules={[{ required: true }]}>
            <Select
              options={assignablePosts.map((post) => ({
                value: post.id,
                label: `${post.name} - ${placementLabels[post.placementScope]}`,
              }))}
              onChange={(id) => {
                if (posts.find((post) => post.id === id)?.placementScope === 'Province') {
                  assignmentForm.setFieldValue('cityId', undefined);
                }
              }}
            />
          </Form.Item>
          <Form.Item name="userId" label="کاربر" rules={[{ required: true }]}>
            <Select
              showSearch
              filterOption={false}
              onSearch={setSearchInput}
              options={userOptions}
              placeholder="جستجوی نام یا نام کاربری"
              notFoundContent="کاربری یافت نشد"
            />
          </Form.Item>
          {auth.scopeLevel === 'Mastery' ? (
            <Form.Item name="provinceId" label="استان" rules={[{ required: true }]}>
              <Select
                options={provinceOptions}
                onChange={() => assignmentForm.setFieldValue('cityId', undefined)}
              />
            </Form.Item>
          ) : (
            <Form.Item name="provinceId" hidden rules={[{ required: true }]}>
              <Input />
            </Form.Item>
          )}
          {!cityAssignmentAllowed ? (
            <Form.Item name="cityId" hidden>
              <Input />
            </Form.Item>
          ) : (
            <Form.Item
              name="cityId"
              label={cityAssignmentRequired ? 'شهرستان' : 'شهرستان (اختیاری)'}
              rules={cityAssignmentRequired ? [{ required: true, message: 'انتخاب شهرستان الزامی است' }] : []}
            >
              <Select
                allowClear={!cityAssignmentRequired}
                disabled={!provinceIdForForm}
                options={cityOptions}
                onChange={(value) => {
                  if (value) {
                    const city = cities.find((item) => item.id === value);
                    if (city) assignmentForm.setFieldValue('provinceId', city.provinceId);
                  }
                }}
              />
            </Form.Item>
          )}
          <Form.Item name="startedOn" label="تاریخ شروع" rules={[{ required: true, message: 'تاریخ شروع الزامی است' }]}>
            <SolarDatePicker placeholder="انتخاب تاریخ شروع" />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={`پایان تخصیص ${endingAssignment?.jobPostName || ''}`}
        open={Boolean(endingAssignment)}
        onCancel={() => setEndingAssignment(null)}
        onOk={() => endForm.submit()}
        confirmLoading={endMutation.isPending}
        okText="ثبت پایان"
        cancelText="انصراف"
        destroyOnClose
      >
        <Form form={endForm} layout="vertical" onFinish={endAssignment}>
          <Form.Item name="endedOn" label="تاریخ پایان" rules={[{ required: true, message: 'تاریخ پایان الزامی است' }]}>
            <SolarDatePicker placeholder="انتخاب تاریخ پایان" />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
