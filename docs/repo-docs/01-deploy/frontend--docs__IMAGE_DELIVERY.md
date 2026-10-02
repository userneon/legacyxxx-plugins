> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/docs/IMAGE_DELIVERY.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/IMAGE_DELIVERY.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# Legacy-X Image Delivery Runbook

## Runtime contract

The application receives **metadata and direct immutable image URLs only**. Catalog, profile, map, and clan endpoints must never stream or proxy image bytes. Nginx, object storage, or a CDN serves `/assets/<content-hash>/...` directly.

Each production image filename must include a content hash, for example `ak47-aphrodite.3ab4187c.webp`. A byte change produces a new URL, so `Cache-Control: public, max-age=31536000, immutable` cannot return stale content. A short-lived `image-manifest.json` maps stable catalog IDs to those versioned URLs.

## Browser loading rules

`OptimizedImage` is the standard raster image component. It supplies width and height, native lazy loading, asynchronous decoding, visible-card priority fetching, and a local SVG fallback that requires no retry request. Catalog requests remain paginated; the browser receives only the 36 current cards rather than an asset catalogue.

Map card images use stable direct `/maps/<map-name>.webp` URLs. In this local Vite preview, those files must live under `/home/ubuntu/webdev-static-assets/maps/` because that directory is the configured `publicDir`; storing them only in the project `public/maps/` directory does not make them available at runtime.

| Asset role | Format | Loading | Cache |
|---|---|---|---|
| Catalog skins, maps, banners, photos | WebP; AVIF may be added later | Lazy; first visible cards priority | 1 year immutable, content-hashed URL |
| SVG, icons, logos, transparent marks | Original SVG/PNG/WebP where appropriate | Eager only when visible | 1 year immutable once hashed |
| Image manifest | JSON | Fetch when catalog metadata changes | 5 minutes, revalidate |
| Broken raster asset | Inline local fallback | No network retry | Not applicable |

## Bulk conversion

Do not place 100,000 assets in the frontend bundle. Store originals and generated files in static object storage or an Nginx-served asset directory. Convert suitable photographic PNG/JPG files with bounded concurrency:

```bash
node scripts/convert-static-images-to-webp.mjs --root /srv/legacyx-assets --quality 84 --jobs 6 --dry-run
node scripts/convert-static-images-to-webp.mjs --root /srv/legacyx-assets --quality 84 --jobs 6
```

The script preserves icons, logos, mark assets, SVGs, sprites, and existing WebP files. It writes adjacent WebP outputs without overwriting originals. Generate the hash-named production copies and manifest as a separate release step, after visual sampling confirms quality.

## Nginx deployment

Include `ops/nginx/legacyx-static-images.conf` inside the production server block after setting the relevant static-storage `root` or `alias`. Do not add an API proxy location for these files. For object storage/CDN, apply the same cache headers at the CDN origin instead.
