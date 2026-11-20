// Shrinks a picture in the browser before upload, so portraits stay small in the database:
// the longest side becomes at most `maxSide` pixels, saved as WebP (or JPEG where the browser
// can't make WebP). Throws if the browser can't read the file as an image.
export async function shrinkImage(file: Blob, maxSide = 600): Promise<Blob> {
  const bitmap = await createImageBitmap(file)
  try {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
    const width = Math.max(1, Math.round(bitmap.width * scale))
    const height = Math.max(1, Math.round(bitmap.height * scale))
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')
    if (!context) throw new Error('This browser cannot resize pictures.')
    context.drawImage(bitmap, 0, 0, width, height)

    const webp = await toBlob(canvas, 'image/webp')
    if (webp?.type === 'image/webp') return webp
    const jpeg = await toBlob(canvas, 'image/jpeg')
    if (!jpeg) throw new Error('This browser cannot resize pictures.')
    return jpeg
  } finally {
    bitmap.close()
  }
}

function toBlob(canvas: HTMLCanvasElement, type: string): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, 0.85))
}
