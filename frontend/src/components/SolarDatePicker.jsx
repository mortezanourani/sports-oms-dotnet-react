import DatePicker from 'react-multi-date-picker';
import persian from 'react-date-object/calendars/persian.js';
import persianFa from 'react-date-object/locales/persian_fa.js';

export default function SolarDatePicker(props) {
    return (
        <DatePicker
            calendar={persian}
            locale={persianFa}
            format="YYYY/MM/DD"
            calendarPosition="bottom-right"
            inputClass="ant-input"
            style={{ width: '100%' }}
            {...props}
        />
    );
}
