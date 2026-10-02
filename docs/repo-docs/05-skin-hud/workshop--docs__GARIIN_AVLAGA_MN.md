> **Хуулбар.** Эх сурвалж: [legacyxxx-workshop/docs/GARIIN_AVLAGA_MN.md](https://github.com/userneon/legacyxxx-workshop/blob/main/docs/GARIIN_AVLAGA_MN.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Workshop: гарын авлага (Монгол)

Энэ repo бол тоглоом доторх LEGACY-X-ийн HUD: зарлал, цолны card, match-ийн EXP, knife-ийн санал хураалт,
`!admin` цэс. Энэ нь **дизайн**, тоглогчийн компьютерт Workshop-оор очно. Юуг хэзээ, хэнд харуулахыг
**plugin** (legacyxxx-plugins) шийднэ.

| Хэн | Юу хийх |
|---|---|
| Claude | XML, CSS, зургийг бичиж, `tools/validate.py`-оор шалгаад push хийнэ |
| Та, **Windows** PC дээр | Build хийж, өөрийн тоглоомд шалгаж, Workshop-д нийтэлнэ |
| Plugin | Текст бөглөх, class асаах, товч дарахыг хүлээж авах (дараагийн ажил) |

## 1. Нэг удаа: Workshop Tools суулгах

1. CS2-ийг нээнэ → **Settings** → хайлтад **Workshop Tools** гэж бичнэ → **Install**.
2. Тоглоомоос гараад Steam татаж дуусахыг хүлээнэ.
3. `Counter-Strike Global Offensive` хавтсанд `game` хавтасны хажууд `content` хавтас гарч ирвэл бэлэн.

## 2. Build хийх (дизайн өөрчлөгдөх бүрт)

```bat
git pull
tools\build.cmd
```

Скрипт шалгаж, `content\csgo_addons\legacyx` руу хуулаад бүгдийг compile хийнэ. Төгсгөлд нь
`All compiled` гарвал амжилттай. `Not compiled:` гарвал тэр жагсаалтыг Claude-д явуулаарай.

## 3. Өөрийн тоглоомд шалгах

```bat
tools\build.cmd -Local
```

Дараа нь CS2-ийг **бүрэн хааж дахин нээнэ**. Panorama layout-ыг session тутамд cache хийдэг тул дахин
нээхгүй бол өөрчлөлт харагдахгүй.

## 4. Workshop-д нийтлэх

1. CS2 Workshop Tools-ийг нээж `legacyx` addon-ыг сонгоно.
2. Workshop-д нийтлэх цонхноос нийтэлнэ. Тоглогчид татаж чадахаар **Public** эсвэл **Unlisted** болгоно.
3. Гарсан **Workshop ID**-г (тоо) Claude-д хэлнэ. Сервер дээр MultiAddonManager-т энэ ID-г тохируулна.

## Анхаарах

* Дизайныг өөрчилсний дараа **дахин нийтлэхгүй бол** тоглогчид хуучин дизайныг харсаар байна. Plugin-ийн
  deploy энд нөлөөлөхгүй.
* Эхний build бол анхны жинхэнэ тест. Юу ч харагдахгүй эсвэл эвдэрсэн байвал CS2-ийн console-ийн (`~`)
  улаан мөрүүдийг хуулж явуулаарай.
