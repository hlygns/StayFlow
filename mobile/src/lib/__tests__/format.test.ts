import { addDays, formatCardNumber, formatExpiry, nightsBetween, toIsoDate } from '../format'

describe('tarih yardimcilari', () => {
  it('yerel tarihi gun kaymadan ISO bicimine cevirir', () => {
    // Gece yarisindan hemen once: toISOString (UTC) kullanilsaydi Turkiye'de bir onceki gune kayardi
    expect(toIsoDate(new Date(2027, 6, 10, 23, 59))).toBe('2027-07-10')
  })

  it('ay ve yil sinirlarini dogru gecer', () => {
    expect(addDays('2027-01-31', 1)).toBe('2027-02-01')
    expect(addDays('2027-12-31', 1)).toBe('2028-01-01')
    expect(addDays('2028-02-28', 1)).toBe('2028-02-29') // artik yil
  })

  it('gece sayisini hesaplar (yaz saati gecisinde de)', () => {
    expect(nightsBetween('2027-07-10', '2027-07-13')).toBe(3)
    expect(nightsBetween('2027-03-27', '2027-03-29')).toBe(2)
  })
})

describe('kart alani bicimlendirme', () => {
  it('kart numarasini 4erli gruplar ve 16 haneyle sinirlar', () => {
    expect(formatCardNumber('4242424242424242')).toBe('4242 4242 4242 4242')
    expect(formatCardNumber('4242-4242 42x')).toBe('4242 4242 42')
    expect(formatCardNumber('42424242424242429999')).toBe('4242 4242 4242 4242')
  })

  it('son kullanma tarihini AA/YY yapar', () => {
    expect(formatExpiry('1')).toBe('1')
    expect(formatExpiry('123')).toBe('12/3')
    expect(formatExpiry('12/30')).toBe('12/30')
  })
})
