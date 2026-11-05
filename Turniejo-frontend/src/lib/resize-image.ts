/**
 * Zmniejsza obrazek w przeglądarce, tak żeby dłuższy bok miał najwyżej `maxSize` px, i zwraca WebP
 * (albo PNG, jeśli przeglądarka nie umie zapisać WebP). Przezroczystość zostaje. Przy okazji SVG
 * zamienia się w zwykły obrazek, więc na serwer nie trafia nic, co mogłoby zawierać skrypt.
 */
export async function resizeImage(file: File, maxSize = 256): Promise<Blob> {
  const url = URL.createObjectURL(file)
  try {
    const image = await loadImage(url)
    const width = image.naturalWidth || maxSize
    const height = image.naturalHeight || maxSize
    const scale = Math.min(1, maxSize / Math.max(width, height))

    const canvas = document.createElement("canvas")
    canvas.width = Math.max(1, Math.round(width * scale))
    canvas.height = Math.max(1, Math.round(height * scale))
    const context = canvas.getContext("2d")
    if (!context) throw new Error("Canvas is not available")
    context.imageSmoothingQuality = "high"
    context.drawImage(image, 0, 0, canvas.width, canvas.height)

    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error("Could not encode the image"))),
        "image/webp",
        0.9,
      ),
    )
  } finally {
    URL.revokeObjectURL(url)
  }
}

function loadImage(src: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image()
    image.onload = () => resolve(image)
    image.onerror = () => reject(new Error("Could not read the image"))
    image.src = src
  })
}
