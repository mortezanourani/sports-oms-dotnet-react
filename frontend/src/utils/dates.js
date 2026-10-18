import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc.js';
import timezone from 'dayjs/plugin/timezone.js';

dayjs.extend(utc);
dayjs.extend(timezone);

const TEHRAN_TIME_ZONE = 'Asia/Tehran';
const solarDateFormatter = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    timeZone: TEHRAN_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
});

export function toSolarPickerValue(value) {
    if (!value) return null;
    const [year, month, day] = dayjs(value).tz(TEHRAN_TIME_ZONE).format('YYYY-MM-DD').split('-').map(Number);
    return new Date(year, month - 1, day, 12);
}

export function toUtcDateTime(value) {
    if (!value) return null;
    const selectedDate = value instanceof Date ? value : value.toDate();
    const gregorianDate = [
        selectedDate.getFullYear(),
        String(selectedDate.getMonth() + 1).padStart(2, '0'),
        String(selectedDate.getDate()).padStart(2, '0'),
    ].join('-');
    return dayjs.tz(`${gregorianDate} 00:00`, TEHRAN_TIME_ZONE).utc().toISOString();
}

export function formatSolarDate(value) {
    if (!value) return '—';
    return solarDateFormatter.format(new Date(value));
}
