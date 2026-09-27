import { Text, View } from 'react-native'
import { styles } from '../../components/ui'
import { space } from '../../lib/theme'

export default function BookingsScreen() {
  return (
    <View style={{ padding: space(4) }}>
      <Text style={styles.muted}>Rezervasyonlarınız burada görünecek.</Text>
    </View>
  )
}
