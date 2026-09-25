export interface CompressedImage {
  blob: Blob
  fileName: string
}

export async function compressImage(
  file: File,
  maxSide = 1600,
  quality = 0.8,
): Promise<CompressedImage> {
  try {
    const bitmap = await createImageBitmap(file)
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
    const width = Math.max(1, Math.round(bitmap.width * scale))
    const height = Math.max(1, Math.round(bitmap.height * scale))

    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height

    const context = canvas.getContext('2d')

    if (!context) {
      bitmap.close()
      return { blob: file, fileName: file.name }
    }

    context.drawImage(bitmap, 0, 0, width, height)
    bitmap.close()

    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', quality),
    )

    if (!blob) {
      return { blob: file, fileName: file.name }
    }

    return { blob, fileName: file.name.replace(/\.[^.]+$/, '') + '.jpg' }
  } catch {
    return { blob: file, fileName: file.name }
  }
}
