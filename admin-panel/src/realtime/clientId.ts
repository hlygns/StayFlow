// Bu tarayici sekmesinin kimligi. Her API istegine X-Client-Id basligiyla eklenir; sunucu canli bildirimde
// degisikligi hangi sekmenin yaptigini geri soyler. Boylece kullanicinin kendi islemi icin ikinci bir
// "Yeni rezervasyon" bildirimi gosterilmez - ayni hesapla acilmis diger sekmeler ise bildirimi alir.
// (Onceki yontem, istegin cevabi gelince isaretliyordu; canli bildirim cevaptan once ulasabildigi icin gec kaliyordu.)

export const CLIENT_ID_HEADER = 'X-Client-Id'

// crypto.randomUUID sadece guvenli baglamda (https/localhost) var; yerel agdan http ile acilirsa yedek
export const CLIENT_ID: string =
  globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`

export function isOwnChange(originClientId: string | null | undefined): boolean {
  return originClientId === CLIENT_ID
}
