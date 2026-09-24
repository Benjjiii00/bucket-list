import axios from 'axios'

export interface DailyForecast {
  date: string
  maxTemp: number
  minTemp: number
  weatherCode: number
  description: string
  icon: string
}

export interface WeatherData {
  temperature: number
  apparentTemperature: number
  humidity: number
  windSpeed: number
  precipitation: number
  weatherCode: number
  description: string
  icon: string
  daily: DailyForecast[]
}

function getWeatherInfo(code: number): { description: string; icon: string } {
  switch (code) {
    case 0:
      return { description: 'Klar / Sonnig', icon: '☀️' }
    case 1:
      return { description: 'Überwiegend sonnig', icon: '🌤️' }
    case 2:
      return { description: 'Teilweise bewölkt', icon: '⛅' }
    case 3:
      return { description: 'Bedeckt', icon: '☁️' }
    case 45:
    case 48:
      return { description: 'Nebel', icon: '🌫️' }
    case 51:
    case 53:
    case 55:
      return { description: 'Leichter Nieselregen', icon: '🌦️' }
    case 61:
    case 63:
    case 65:
      return { description: 'Regen', icon: '🌧️' }
    case 71:
    case 73:
    case 75:
    case 77:
      return { description: 'Schneefall', icon: '🌨️' }
    case 80:
    case 81:
    case 82:
      return { description: 'Regenschauer', icon: '🌦️' }
    case 85:
    case 86:
      return { description: 'Schneeschauer', icon: '🌨️' }
    case 95:
    case 96:
    case 99:
      return { description: 'Gewitter', icon: '⛈️' }
    default:
      return { description: 'Heiter bis wolkig', icon: '🌤️' }
  }
}

class WeatherService {
  async getWeather(lat: number, lon: number): Promise<WeatherData | null> {
    try {
      const url = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto`
      const response = await axios.get(url, { timeout: 4000 })
      const data = response.data

      const current = data.current
      const info = getWeatherInfo(current.weather_code)

      const daily: DailyForecast[] = []
      if (data.daily && data.daily.time) {
        for (let i = 0; i < Math.min(data.daily.time.length, 4); i++) {
          const dayInfo = getWeatherInfo(data.daily.weather_code[i])
          daily.push({
            date: data.daily.time[i],
            maxTemp: Math.round(data.daily.temperature_2m_max[i]),
            minTemp: Math.round(data.daily.temperature_2m_min[i]),
            weatherCode: data.daily.weather_code[i],
            description: dayInfo.description,
            icon: dayInfo.icon,
          })
        }
      }

      return {
        temperature: Math.round(current.temperature_2m),
        apparentTemperature: Math.round(current.apparent_temperature),
        humidity: current.relative_humidity_2m,
        windSpeed: Math.round(current.wind_speed_10m),
        precipitation: current.precipitation ?? 0,
        weatherCode: current.weather_code,
        description: info.description,
        icon: info.icon,
        daily,
      }
    } catch {
      return null
    }
  }
}

export default new WeatherService()
